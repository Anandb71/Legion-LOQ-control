using LegionLoqControl.Application.Diagnostics;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Services;
using LegionLoqControl.ViewModels;
using Xunit;

namespace LegionLoqControl.Presentation.Tests;

public sealed class DiagnosticsExportViewModelTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Export_is_disabled_until_inventory_is_retained()
    {
        var session = new MachineSessionViewModel();
        using DiagnosticsExportViewModel viewModel = CreateViewModel(
            session,
            new StubWriter(),
            new StubPicker(@"C:\exports\diagnostics.json"));

        Assert.False(viewModel.ExportCommand.CanExecute(null));

        session.UpdateMachineSnapshot(CreateMachineSnapshot());

        Assert.True(viewModel.ExportCommand.CanExecute(null));
        Assert.Contains("no new hardware read", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Dialog_cancellation_writes_nothing()
    {
        var session = new MachineSessionViewModel();
        session.UpdateMachineSnapshot(CreateMachineSnapshot());
        var writer = new StubWriter();
        var picker = new StubPicker(destinationPath: null);
        using DiagnosticsExportViewModel viewModel =
            CreateViewModel(session, writer, picker);

        await viewModel.ExportCommand.ExecuteAsync(null);

        Assert.Equal(1, picker.PickCount);
        Assert.Equal(0, writer.WriteCount);
        Assert.Equal("Export cancelled · no file created", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Export_uses_only_retained_snapshots_and_never_discloses_the_path()
    {
        var session = new MachineSessionViewModel();
        MachineSnapshot machine = CreateMachineSnapshot();
        HardwareStateSnapshot hardware = CreateHardwareSnapshot();
        session.UpdateMachineSnapshot(machine);
        session.UpdateHardwareStateSnapshot(hardware);
        var writer = new StubWriter();
        const string destination = @"C:\Users\private-name\diagnostics.json";
        using DiagnosticsExportViewModel viewModel = CreateViewModel(
            session,
            writer,
            new StubPicker(destination));

        await viewModel.ExportCommand.ExecuteAsync(null);

        Assert.Equal(1, writer.WriteCount);
        Assert.Equal(destination, writer.DestinationPath);
        Assert.Equal(DiagnosticsExportWriteMode.ReplaceExisting, writer.Mode);
        Assert.NotNull(writer.Document);
        Assert.Equal(
            DiagnosticsHardwareCaptureStatus.Captured,
            writer.Document.HardwareState.CaptureStatus);
        Assert.Equal("conservation", writer.Document.HardwareState.BatteryChargeMode!.Value);
        Assert.DoesNotContain(
            "private-name",
            viewModel.StatusMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "Diagnostics exported · review the JSON before sharing",
            viewModel.StatusMessage);
    }

    [Fact]
    public async Task Stable_writer_failure_is_mapped_without_exception_details()
    {
        var session = new MachineSessionViewModel();
        session.UpdateMachineSnapshot(CreateMachineSnapshot());
        var writer = new StubWriter
        {
            Exception = new DiagnosticsExportException(
                "diagnostics_export_access_denied",
                new UnauthorizedAccessException(@"C:\Users\private-name")),
        };
        using DiagnosticsExportViewModel viewModel = CreateViewModel(
            session,
            writer,
            new StubPicker(@"C:\Users\private-name\diagnostics.json"));

        await viewModel.ExportCommand.ExecuteAsync(null);

        Assert.Equal("Export blocked by Windows file permissions", viewModel.StatusMessage);
        Assert.DoesNotContain(
            "private-name",
            viewModel.StatusMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    private static DiagnosticsExportViewModel CreateViewModel(
        MachineSessionViewModel session,
        StubWriter writer,
        StubPicker picker) =>
        new(
            session,
            new DiagnosticsExportService(new FixedTimeProvider(Now)),
            writer,
            picker,
            "1.0.0");

    private static MachineSnapshot CreateMachineSnapshot() =>
        new(
            new MachineIdentity(
                Observation.FromValue("LENOVO"),
                Observation.FromValue("LOQ 15IRX9"),
                Observation.FromValue("LOQ 15IRX9"),
                Observation.FromValue("83DV"),
                Observation.FromValue("NECN50WW")),
            Now,
            []);

    private static HardwareStateSnapshot CreateHardwareSnapshot() =>
        new(
            Now,
            HardwareReadResult<BatteryChargeMode>.Success(
                BatteryChargeMode.Conservation),
            HardwareReadResult<ThermalMode>.Success(ThermalMode.Performance),
            HardwareReadResult<ToggleState>.Success(ToggleState.Disabled),
            HardwareReadResult<IntegratedGpuMode>.Success(
                IntegratedGpuMode.IntegratedOnly),
            HardwareReadResult<FourZoneKeyboardMode>.Success(
                FourZoneKeyboardMode.Unknown),
            HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.Unavailable,
                "fan_table_not_opened"));

    private sealed class StubPicker(string? destinationPath)
        : IDiagnosticsExportDestinationPicker
    {
        public int PickCount { get; private set; }

        public string? PickDestination()
        {
            PickCount++;
            return destinationPath;
        }
    }

    private sealed class StubWriter : IDiagnosticsExportWriter
    {
        public int WriteCount { get; private set; }

        public DiagnosticsExportDocument? Document { get; private set; }

        public string? DestinationPath { get; private set; }

        public DiagnosticsExportWriteMode? Mode { get; private set; }

        public Exception? Exception { get; init; }

        public ValueTask WriteAsync(
            DiagnosticsExportDocument document,
            string destinationPath,
            DiagnosticsExportWriteMode mode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCount++;
            if (Exception is not null)
                return ValueTask.FromException(Exception);

            Document = document;
            DestinationPath = destinationPath;
            Mode = mode;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
