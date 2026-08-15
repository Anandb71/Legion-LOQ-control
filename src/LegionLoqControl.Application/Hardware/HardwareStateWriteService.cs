using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Application.Hardware;

public enum HardwareWriteKind
{
    ThermalMode = 0,
    DisplayOverdrive = 1,
    IntegratedGpuMode = 2,
    BatteryChargeMode = 3,
    FourZoneKeyboard = 4,
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

    ValueTask WriteBatteryChargeModeAsync(
        BatteryChargeMode expected,
        BatteryChargeMode desired,
        CancellationToken cancellationToken);

    ValueTask WriteFourZoneKeyboardAsync(
        FourZoneKeyboardMode desired,
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
            case HardwareWriteKind.IntegratedGpuMode:
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
            case HardwareWriteKind.BatteryChargeMode:
                BatteryChargeMode expectedBattery = ParseBattery(expected);
                BatteryChargeMode desiredBattery = ParseBattery(desired);
                EnsureMatch(
                    before.BatteryChargeMode,
                    expectedBattery,
                    "battery_expected_mismatch");
                if (expectedBattery == desiredBattery)
                    return before;

                await _writer
                    .WriteBatteryChargeModeAsync(
                        expectedBattery,
                        desiredBattery,
                        cancellationToken)
                    .ConfigureAwait(false);
                return await VerifyAsync(
                    snapshot => snapshot.BatteryChargeMode,
                    desiredBattery,
                    "battery_readback_mismatch",
                    cancellationToken)
                    .ConfigureAwait(false);
            case HardwareWriteKind.FourZoneKeyboard:
                FourZoneKeyboardMode expectedKeyboard = ParseKeyboard(expected);
                FourZoneKeyboardMode desiredKeyboard = ParseKeyboard(desired);
                if (desiredKeyboard == FourZoneKeyboardMode.Unknown)
                {
                    throw new HardwareWriteException(
                        "keyboard_value_invalid",
                        HardwareWriteStatus.Failed);
                }

                EnsureKeyboardPresence(before.FourZoneKeyboard, expectedKeyboard);
                await _writer
                    .WriteFourZoneKeyboardAsync(desiredKeyboard, cancellationToken)
                    .ConfigureAwait(false);
                return await VerifyKeyboardAsync(desiredKeyboard, cancellationToken)
                    .ConfigureAwait(false);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
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

    private static BatteryChargeMode ParseBattery(string value) =>
        ParseEnum<BatteryChargeMode>(value, "battery_value_invalid");

    private static FourZoneKeyboardMode ParseKeyboard(string value) =>
        ParseEnum<FourZoneKeyboardMode>(value, "keyboard_value_invalid");

    private static void EnsureKeyboardPresence(
        HardwareReadResult<FourZoneKeyboardMode> result,
        FourZoneKeyboardMode expected)
    {
        if (result.Status != HardwareReadStatus.Success || !result.Value.HasValue)
        {
            throw new HardwareWriteException(
                "keyboard_expected_mismatch",
                HardwareWriteStatus.Conflict);
        }

        if (expected != FourZoneKeyboardMode.Unknown &&
            result.Value.Value != FourZoneKeyboardMode.Unknown &&
            result.Value.Value != expected)
        {
            throw new HardwareWriteException(
                "keyboard_expected_mismatch",
                HardwareWriteStatus.Conflict);
        }
    }

    private async ValueTask<HardwareStateSnapshot> VerifyKeyboardAsync(
        FourZoneKeyboardMode desired,
        CancellationToken cancellationToken)
    {
        HardwareStateSnapshot after = await CaptureAsync(cancellationToken).ConfigureAwait(false);
        HardwareReadResult<FourZoneKeyboardMode> result = after.FourZoneKeyboard;
        if (result.Status != HardwareReadStatus.Success || !result.Value.HasValue)
        {
            throw new HardwareWriteException(
                "keyboard_readback_mismatch",
                HardwareWriteStatus.Unverified);
        }

        if (result.Value.Value is FourZoneKeyboardMode.Unknown || result.Value.Value == desired)
            return after;

        throw new HardwareWriteException(
            "keyboard_readback_mismatch",
            HardwareWriteStatus.Unverified);
    }

    private static T ParseEnum<T>(string value, string errorCode)
        where T : struct, Enum
    {
        if (!Enum.TryParse(value, ignoreCase: true, out T parsed) || !Enum.IsDefined(parsed))
            throw new HardwareWriteException(errorCode, HardwareWriteStatus.Failed);
        return parsed;
    }
}
