using System.Management;
using System.Runtime.InteropServices;
using LegionLoqControl.Application.Abstractions;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Infrastructure.Windows.Management;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Diagnostics;

public sealed class WindowsMachineIdentitySource : IMachineIdentitySource
{
    private const string CimNamespace = @"root\CIMV2";
    private readonly IWindowsManagementReader _reader;

    public WindowsMachineIdentitySource()
        : this(new SystemManagementReader())
    {
    }

    internal WindowsMachineIdentitySource(IWindowsManagementReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public string SourceName => "windows.wmi.identity";

    public ValueTask<MachineIdentity> ReadAsync(CancellationToken cancellationToken)
    {
        WindowsPlatform.EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MachineIdentity>(Task.Run(() => Read(cancellationToken), cancellationToken));
    }

    private MachineIdentity Read(CancellationToken cancellationToken)
    {
        ReadAttempt product = TryRead(
            "Win32_ComputerSystemProduct",
            ["Vendor", "Name", "Version"]);
        cancellationToken.ThrowIfCancellationRequested();

        ReadAttempt system = TryRead(
            "Win32_ComputerSystem",
            ["Manufacturer", "Model", "SystemFamily"]);
        cancellationToken.ThrowIfCancellationRequested();

        ReadAttempt bios = TryRead("Win32_BIOS", ["SMBIOSBIOSVersion"]);

        return new MachineIdentity(
            Resolve(
                new FieldCandidate(product, "Vendor"),
                new FieldCandidate(system, "Manufacturer")),
            Resolve(
                new FieldCandidate(system, "SystemFamily"),
                new FieldCandidate(product, "Version")),
            Resolve(
                new FieldCandidate(product, "Version"),
                new FieldCandidate(system, "SystemFamily"),
                new FieldCandidate(system, "Model")),
            Resolve(
                new FieldCandidate(product, "Name"),
                new FieldCandidate(system, "Model")),
            Resolve(new FieldCandidate(bios, "SMBIOSBIOSVersion")));
    }

    private ReadAttempt TryRead(string className, IReadOnlyCollection<string> properties)
    {
        try
        {
            IReadOnlyDictionary<string, string?> values = _reader.ReadFirstInstance(
                CimNamespace,
                className,
                properties);
            return new ReadAttempt(values, null, null);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failed("wmi_access_denied", exception);
        }
        catch (ManagementException exception)
        {
            return Failed("wmi_management_error", exception);
        }
        catch (COMException exception)
        {
            return Failed("wmi_com_error", exception);
        }
        catch (Exception exception)
        {
            return Failed("wmi_query_failed", exception);
        }
    }

    private static ReadAttempt Failed(string errorCode, Exception exception) =>
        new(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            errorCode,
            exception.GetType().Name);

    private static Observation Resolve(params FieldCandidate[] candidates)
    {
        foreach (FieldCandidate candidate in candidates)
        {
            if (candidate.Attempt.Values.TryGetValue(candidate.PropertyName, out string? value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return Observation.FromValue(value);
            }
        }

        ReadAttempt? failure = candidates
            .Select(static candidate => candidate.Attempt)
            .FirstOrDefault(static attempt => attempt.ErrorCode is not null);

        return failure?.ErrorCode is { } errorCode
            ? Observation.Failed(errorCode, failure.ErrorDetail)
            : Observation.Unavailable("wmi_value_missing");
    }

    private sealed record ReadAttempt(
        IReadOnlyDictionary<string, string?> Values,
        string? ErrorCode,
        string? ErrorDetail);

    private readonly record struct FieldCandidate(ReadAttempt Attempt, string PropertyName);
}
