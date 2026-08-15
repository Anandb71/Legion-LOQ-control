using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Application.Hardware;

public enum HardwareWriteKind
{
    ThermalMode = 0,
    DisplayOverdrive = 1,
    IntegratedGpuMode = 2,
}

public enum HardwareWriteStatus
{
    Succeeded = 0,
    Unsupported = 1,
    Conflict = 2,
    Unverified = 3,
    Failed = 4,
}

public sealed class HardwareWriteException : Exception
{
    public HardwareWriteException(
        string errorCode,
        HardwareWriteStatus status = HardwareWriteStatus.Failed)
        : base(ValidateErrorCode(errorCode))
    {
        ErrorCode = errorCode.Trim();
        Status = status;
    }

    public string ErrorCode { get; }

    public HardwareWriteStatus Status { get; }

    private static string ValidateErrorCode(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        return errorCode.Trim();
    }
}

public interface IHardwareStateWriter
{
    ValueTask WriteThermalModeAsync(ThermalMode desired, CancellationToken cancellationToken);

    ValueTask WriteDisplayOverdriveAsync(ToggleState desired, CancellationToken cancellationToken);

    ValueTask WriteIntegratedGpuModeAsync(
        IntegratedGpuMode desired,
        CancellationToken cancellationToken);
}

public sealed class HardwareStateWriteService
{
    private readonly Func<IHardwareStateReader> _createReader;
    private readonly IHardwareStateWriter _writer;

    public HardwareStateWriteService(
        Func<IHardwareStateReader> createReader,
        IHardwareStateWriter writer)
    {
        _createReader = createReader ?? throw new ArgumentNullException(nameof(createReader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public async ValueTask<HardwareStateSnapshot> ApplyAsync(
        HardwareWriteKind kind,
        string expected,
        string desired,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expected);
        ArgumentException.ThrowIfNullOrWhiteSpace(desired);
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        HardwareStateSnapshot before = await CaptureAsync(cancellationToken).ConfigureAwait(false);
        switch (kind)
        {
            case HardwareWriteKind.ThermalMode:
                ThermalMode expectedThermal = ParseThermal(expected);
                ThermalMode desiredThermal = ParseThermal(desired);
                if (desiredThermal == ThermalMode.Custom)
                {
                    throw new HardwareWriteException(
                        "thermal_custom_unsupported",
                        HardwareWriteStatus.Unsupported);
                }

                EnsureMatch(before.ThermalMode, expectedThermal, "thermal_expected_mismatch");
                await _writer
                    .WriteThermalModeAsync(desiredThermal, cancellationToken)
                    .ConfigureAwait(false);
                return await VerifyAsync(
                    snapshot => snapshot.ThermalMode,
                    desiredThermal,
                    "thermal_readback_mismatch",
                    cancellationToken)
                    .ConfigureAwait(false);
            case HardwareWriteKind.DisplayOverdrive:
                ToggleState expectedOverdrive = ParseToggle(expected);
                ToggleState desiredOverdrive = ParseToggle(desired);
                EnsureMatch(
                    before.DisplayOverdrive,
                    expectedOverdrive,
                    "overdrive_expected_mismatch");
                await _writer
                    .WriteDisplayOverdriveAsync(desiredOverdrive, cancellationToken)
                    .ConfigureAwait(false);
                return await VerifyAsync(
                    snapshot => snapshot.DisplayOverdrive,
                    desiredOverdrive,
                    "overdrive_readback_mismatch",
                    cancellationToken)
                    .ConfigureAwait(false);
            default:
                IntegratedGpuMode expectedGpu = ParseGpu(expected);
                IntegratedGpuMode desiredGpu = ParseGpu(desired);
                EnsureMatch(
                    before.IntegratedGpuMode,
                    expectedGpu,
                    "integrated_gpu_expected_mismatch");
                await _writer
                    .WriteIntegratedGpuModeAsync(desiredGpu, cancellationToken)
                    .ConfigureAwait(false);
                return await VerifyAsync(
                    snapshot => snapshot.IntegratedGpuMode,
                    desiredGpu,
                    "integrated_gpu_readback_mismatch",
                    cancellationToken)
                    .ConfigureAwait(false);
        }
    }

    private async ValueTask<HardwareStateSnapshot> CaptureAsync(
        CancellationToken cancellationToken) =>
        await new HardwareStateService(_createReader())
            .CaptureAsync(cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask<HardwareStateSnapshot> VerifyAsync<T>(
        Func<HardwareStateSnapshot, HardwareReadResult<T>> selector,
        T desired,
        string errorCode,
        CancellationToken cancellationToken)
        where T : struct
    {
        HardwareStateSnapshot after = await CaptureAsync(cancellationToken).ConfigureAwait(false);
        HardwareReadResult<T> result = selector(after);
        if (result.Status != HardwareReadStatus.Success ||
            !result.Value.HasValue ||
            !EqualityComparer<T>.Default.Equals(result.Value.Value, desired))
        {
            throw new HardwareWriteException(errorCode, HardwareWriteStatus.Unverified);
        }

        return after;
    }

    private static void EnsureMatch<T>(
        HardwareReadResult<T> result,
        T expected,
        string errorCode)
        where T : struct
    {
        if (result.Status != HardwareReadStatus.Success ||
            !result.Value.HasValue ||
            !EqualityComparer<T>.Default.Equals(result.Value.Value, expected))
        {
            throw new HardwareWriteException(errorCode, HardwareWriteStatus.Conflict);
        }
    }

    private static ThermalMode ParseThermal(string value) =>
        ParseEnum<ThermalMode>(value, "thermal_value_invalid");

    private static ToggleState ParseToggle(string value) =>
        ParseEnum<ToggleState>(value, "overdrive_value_invalid");

    private static IntegratedGpuMode ParseGpu(string value) =>
        ParseEnum<IntegratedGpuMode>(value, "integrated_gpu_value_invalid");

    private static T ParseEnum<T>(string value, string errorCode)
        where T : struct, Enum
    {
        if (!Enum.TryParse(value, ignoreCase: true, out T parsed) || !Enum.IsDefined(parsed))
            throw new HardwareWriteException(errorCode, HardwareWriteStatus.Failed);
        return parsed;
    }
}
