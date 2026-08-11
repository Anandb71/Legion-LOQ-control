using System.Text;
using System.Text.Json;
using LegionLoqControl.Application.Diagnostics;
using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Infrastructure.Windows.Diagnostics;
using Xunit;

namespace LegionLoqControl.Platform.Tests;

public sealed class DiagnosticsJsonExportTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Serializer_emits_only_the_explicit_allowlisted_contract()
    {
        DiagnosticsExportDocument document = CreateDocument(
            detail: @"secret=C:\Users\anand devicePath=\\?\HID#nonce");

        byte[] content = DiagnosticsJsonSerializer.Serialize(document);
        string json = Encoding.UTF8.GetString(content);
        using JsonDocument parsed = JsonDocument.Parse(content);

        Assert.Equal((byte)'{', content[0]);
        Assert.Equal(
            "legion-loq-control-diagnostics",
            parsed.RootElement.GetProperty("documentType").GetString());
        Assert.Equal(1, parsed.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            "notCaptured",
            parsed.RootElement
                .GetProperty("hardwareState")
                .GetProperty("captureStatus")
                .GetString());
        Assert.Contains(
            "\"capability\": \"thermalMode\"",
            json,
            StringComparison.Ordinal);
        Assert.DoesNotContain("\"detail\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"source\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"hasValue\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\Users\anand", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nonce", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Writer_creates_and_atomically_replaces_complete_json()
    {
        using var temporary = new TemporaryDirectory();
        var writer = new JsonDiagnosticsExportWriter();
        DiagnosticsExportDocument first = CreateDocument(detail: null);
        string destination = Path.Combine(temporary.DirectoryPath, "diagnostics.json");

        await writer.WriteAsync(
            first,
            destination,
            DiagnosticsExportWriteMode.CreateNew,
            TestContext.Current.CancellationToken);

        string initial = await File.ReadAllTextAsync(
            destination,
            TestContext.Current.CancellationToken);
        DiagnosticsExportDocument replacement = first with { ProductVersion = "2.0.0" };
        await writer.WriteAsync(
            replacement,
            destination,
            DiagnosticsExportWriteMode.ReplaceExisting,
            TestContext.Current.CancellationToken);
        string replaced = await File.ReadAllTextAsync(
            destination,
            TestContext.Current.CancellationToken);

        Assert.Contains("\"productVersion\": \"1.0.0\"", initial, StringComparison.Ordinal);
        Assert.Contains("\"productVersion\": \"2.0.0\"", replaced, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(temporary.DirectoryPath, "*.tmp"));
    }

    [Fact]
    public async Task Create_new_refuses_to_overwrite_an_existing_export()
    {
        using var temporary = new TemporaryDirectory();
        string destination = Path.Combine(temporary.DirectoryPath, "diagnostics.json");
        await File.WriteAllTextAsync(
            destination,
            "original",
            TestContext.Current.CancellationToken);
        var writer = new JsonDiagnosticsExportWriter();

        DiagnosticsExportException exception = await Assert.ThrowsAsync<DiagnosticsExportException>(
            () => writer.WriteAsync(
                CreateDocument(detail: null),
                destination,
                DiagnosticsExportWriteMode.CreateNew,
                TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("diagnostics_export_destination_exists", exception.ErrorCode);
        Assert.Equal(
            "original",
            await File.ReadAllTextAsync(
                destination,
                TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(temporary.DirectoryPath, "*.tmp"));
    }

    [Fact]
    public async Task Cancelled_export_preserves_the_existing_destination()
    {
        using var temporary = new TemporaryDirectory();
        string destination = Path.Combine(temporary.DirectoryPath, "diagnostics.json");
        await File.WriteAllTextAsync(
            destination,
            "original",
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var writer = new JsonDiagnosticsExportWriter();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => writer.WriteAsync(
                CreateDocument(detail: null),
                destination,
                DiagnosticsExportWriteMode.ReplaceExisting,
                cancellation.Token).AsTask());

        Assert.Equal(
            "original",
            await File.ReadAllTextAsync(
                destination,
                TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(temporary.DirectoryPath, "*.tmp"));
    }

    [Fact]
    public async Task Relative_destination_is_rejected_without_creating_a_file()
    {
        var writer = new JsonDiagnosticsExportWriter();

        DiagnosticsExportException exception = await Assert.ThrowsAsync<DiagnosticsExportException>(
            () => writer.WriteAsync(
                CreateDocument(detail: null),
                "diagnostics.json",
                DiagnosticsExportWriteMode.CreateNew,
                TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("diagnostics_export_destination_invalid", exception.ErrorCode);
    }

    private static DiagnosticsExportDocument CreateDocument(string? detail)
    {
        var machine = new MachineSnapshot(
            new MachineIdentity(
                Observation.FromValue("LENOVO"),
                Observation.FromValue("LOQ 15IRX9"),
                Observation.FromValue("LOQ 15IRX9"),
                Observation.FromValue("83DV"),
                Observation.FromValue("NECN50WW")),
            Now,
            [
                new CapabilityEvidence(
                    HardwareCapability.ThermalMode,
                    CapabilitySupport.Unknown,
                    "windows.wmi.metadata",
                    Now,
                    "wmi_interface_present_unverified",
                    detail),
            ]);
        var service = new DiagnosticsExportService(new FixedTimeProvider(Now));
        return service.Create(machine, retainedHardwareState: null, productVersion: "1.0.0");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                $"LegionLoqControl-diagnostics-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
    }
}
