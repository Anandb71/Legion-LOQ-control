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
    OvernightCharge = 5,
    FnLock = 6,
    AlwaysOnUsb = 7,
    TouchpadLock = 8,
    WinKeyLock = 9,
    FourZoneLighting = 10,
    FanTable = 11,
    SpectrumKeyboard = 12,
}

public enum HardwareWriteStatus
{
    Succeeded = 0,
    Unsupported = 1,
    Conflict = 2,
    Unverified = 3,
    Failed = 4,
    Busy = 5,
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

    ValueTask WriteOvernightChargeAsync(
        ToggleState desired,
        CancellationToken cancellationToken) =>
        throw new HardwareWriteException("overnight_not_implemented", HardwareWriteStatus.Unsupported);

    ValueTask WriteFnLockAsync(
        ToggleState desired,
        CancellationToken cancellationToken) =>
        throw new HardwareWriteException("fn_lock_not_implemented", HardwareWriteStatus.Unsupported);

    ValueTask WriteAlwaysOnUsbAsync(
        AlwaysOnUsbState desired,
        CancellationToken cancellationToken) =>
        throw new HardwareWriteException("always_on_usb_not_implemented", HardwareWriteStatus.Unsupported);

    ValueTask WriteTouchpadLockAsync(
        ToggleState desired,
        CancellationToken cancellationToken) =>
        throw new HardwareWriteException("touchpad_lock_not_implemented", HardwareWriteStatus.Unsupported);

    ValueTask WriteWinKeyLockAsync(
        ToggleState desired,
        CancellationToken cancellationToken) =>
        throw new HardwareWriteException("win_key_lock_not_implemented", HardwareWriteStatus.Unsupported);

    ValueTask WriteFourZoneLightingAsync(
        FourZoneLightingState desired,
        CancellationToken cancellationToken) =>
        throw new HardwareWriteException("four_zone_lighting_not_implemented", HardwareWriteStatus.Unsupported);

    ValueTask WriteFanTableAsync(
        FanTableSnapshot desired,
        CancellationToken cancellationToken) =>
        throw new HardwareWriteException("fan_table_not_implemented", HardwareWriteStatus.Unsupported);

    ValueTask WriteSpectrumKeyboardAsync(
        SpectrumBrightness desired,
        CancellationToken cancellationToken) =>
        throw new HardwareWriteException("spectrum_not_implemented", HardwareWriteStatus.Unsupported);
}

public sealed class HardwareStateWriteService
{
    public const int MaximumBatchSize = 2;

    private readonly Func<IHardwareStateReader> _createReader;
    private readonly IHardwareStateWriter _writer;
    private readonly HardwareWriteGate _gate;
    private readonly IHardwareWriteJournal _journal;
    private readonly TimeProvider _timeProvider;

    public HardwareStateWriteService(
        Func<IHardwareStateReader> createReader,
        IHardwareStateWriter writer,
        HardwareWriteGate? gate = null,
        IHardwareWriteJournal? journal = null,
        TimeProvider? timeProvider = null)
    {
        _createReader = createReader ?? throw new ArgumentNullException(nameof(createReader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _gate = gate ?? new HardwareWriteGate();
        _journal = journal ?? new HardwareWriteJournal();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<HardwareStateSnapshot> ApplyAsync(
        HardwareWriteKind kind,
        string expected,
        string desired,
        CancellationToken cancellationToken = default)
    {
        using IDisposable lease = await _gate.EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            HardwareStateSnapshot snapshot = await ApplyCoreAsync(
                    kind,
                    expected,
                    desired,
                    cancellationToken)
                .ConfigureAwait(false);
            _journal.Append(new HardwareWriteJournalEntry(
                _timeProvider.GetUtcNow(),
                kind,
                expected.Trim(),
                desired.Trim(),
                HardwareWriteStatus.Succeeded,
                ErrorCode: null));
            return snapshot;
        }
        catch (HardwareWriteException exception)
        {
            _journal.Append(new HardwareWriteJournalEntry(
                _timeProvider.GetUtcNow(),
                kind,
                expected.Trim(),
                desired.Trim(),
                exception.Status,
                exception.ErrorCode));
            throw;
        }
    }

    public async ValueTask<HardwareStateSnapshot> ApplyManyAsync(
        IReadOnlyList<(HardwareWriteKind Kind, string Expected, string Desired)> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Count is < 1 or > MaximumBatchSize)
        {
            throw new HardwareWriteException(
                "write_batch_invalid",
                HardwareWriteStatus.Failed);
        }

        HashSet<HardwareWriteKind> kinds = [];
        foreach ((HardwareWriteKind kind, string expected, string desired) in operations)
        {
            if (!Enum.IsDefined(kind) ||
                string.IsNullOrWhiteSpace(expected) ||
                string.IsNullOrWhiteSpace(desired) ||
                !kinds.Add(kind))
            {
                throw new HardwareWriteException(
                    "write_batch_invalid",
                    HardwareWriteStatus.Failed);
            }
        }

        using IDisposable lease = await _gate.EnterAsync(cancellationToken).ConfigureAwait(false);
        HardwareStateSnapshot? last = null;
        foreach ((HardwareWriteKind kind, string expected, string desired) in operations)
        {
            try
            {
                last = await ApplyCoreAsync(kind, expected, desired, cancellationToken)
                    .ConfigureAwait(false);
                _journal.Append(new HardwareWriteJournalEntry(
                    _timeProvider.GetUtcNow(),
                    kind,
                    expected.Trim(),
                    desired.Trim(),
                    HardwareWriteStatus.Succeeded,
                    ErrorCode: null));
            }
            catch (HardwareWriteException exception)
            {
                _journal.Append(new HardwareWriteJournalEntry(
                    _timeProvider.GetUtcNow(),
                    kind,
                    expected.Trim(),
                    desired.Trim(),
                    exception.Status,
                    exception.ErrorCode));
                throw;
            }
        }

        return last!;
    }

    private async ValueTask<HardwareStateSnapshot> ApplyCoreAsync(
        HardwareWriteKind kind,
        string expected,
        string desired,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expected);
        ArgumentException.ThrowIfNullOrWhiteSpace(desired);
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        HardwareStateSnapshot before = await CaptureAsync(cancellationToken).ConfigureAwait(false);
        switch (kind)
        {
            case HardwareWriteKind.ThermalMode:
                _ = ParseThermal(expected);
                ThermalMode desiredThermal = ParseThermal(desired);
                if (desiredThermal == ThermalMode.Custom)
                {
                    throw new HardwareWriteException(
                        "thermal_custom_unsupported",
                        HardwareWriteStatus.Unsupported);
                }

                ThermalMode currentThermal = RequireCurrent(
                    before.ThermalMode,
                    "thermal_expected_mismatch");
                if (currentThermal == desiredThermal)
                    return before;

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
                _ = ParseToggle(expected);
                ToggleState desiredOverdrive = ParseToggle(desired);
                ToggleState currentOverdrive = RequireCurrent(
                    before.DisplayOverdrive,
                    "overdrive_expected_mismatch");
                if (currentOverdrive == desiredOverdrive)
                    return before;

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
                _ = ParseGpu(expected);
                IntegratedGpuMode desiredGpu = ParseGpu(desired);
                IntegratedGpuMode currentGpu = RequireCurrent(
                    before.IntegratedGpuMode,
                    "integrated_gpu_expected_mismatch");
                if (currentGpu == desiredGpu)
                    return before;

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
                _ = ParseBattery(expected);
                BatteryChargeMode desiredBattery = ParseBattery(desired);
                BatteryChargeMode currentBattery = RequireCurrent(
                    before.BatteryChargeMode,
                    "battery_expected_mismatch");
                if (currentBattery == desiredBattery)
                    return before;

                await _writer
                    .WriteBatteryChargeModeAsync(
                        currentBattery,
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
                _ = ParseKeyboard(expected);
                FourZoneKeyboardMode desiredKeyboard = ParseKeyboard(desired);
                if (desiredKeyboard == FourZoneKeyboardMode.Unknown)
                {
                    throw new HardwareWriteException(
                        "keyboard_value_invalid",
                        HardwareWriteStatus.Failed);
                }

                RequireKeyboardPresence(before.FourZoneKeyboard);
                await _writer
                    .WriteFourZoneKeyboardAsync(desiredKeyboard, cancellationToken)
                    .ConfigureAwait(false);
                return await VerifyKeyboardAsync(desiredKeyboard, cancellationToken)
                    .ConfigureAwait(false);
            case HardwareWriteKind.OvernightCharge:
                _ = ParseToggle(expected);
                ToggleState desiredOvernight = ParseToggle(desired);
                ToggleState currentOvernight = RequireCurrent(
                    before.OvernightCharge,
                    "overnight_expected_mismatch");
                if (currentOvernight == desiredOvernight)
                    return before;

                await _writer
                    .WriteOvernightChargeAsync(desiredOvernight, cancellationToken)
                    .ConfigureAwait(false);
                return await VerifyAsync(
                    snapshot => snapshot.OvernightCharge,
                    desiredOvernight,
                    "overnight_readback_mismatch",
                    cancellationToken)
                    .ConfigureAwait(false);
            case HardwareWriteKind.FnLock:
                _ = ParseToggle(expected);
                ToggleState desiredFnLock = ParseToggle(desired);
                ToggleState currentFnLock = RequireCurrent(
                    before.FnLock,
                    "fn_lock_expected_mismatch");
                if (currentFnLock == desiredFnLock)
                    return before;

                await _writer
                    .WriteFnLockAsync(desiredFnLock, cancellationToken)
                    .ConfigureAwait(false);
                return await VerifyAsync(
                    snapshot => snapshot.FnLock,
                    desiredFnLock,
                    "fn_lock_readback_mismatch",
                    cancellationToken)
                    .ConfigureAwait(false);
            case HardwareWriteKind.AlwaysOnUsb:
                _ = ParseAlwaysOnUsb(expected);
                AlwaysOnUsbState desiredUsb = ParseAlwaysOnUsb(desired);
                AlwaysOnUsbState currentUsb = RequireCurrent(
                    before.AlwaysOnUsb,
                    "always_on_usb_expected_mismatch");
                if (currentUsb == desiredUsb)
                    return before;

                await _writer
                    .WriteAlwaysOnUsbAsync(desiredUsb, cancellationToken)
                    .ConfigureAwait(false);
                return await VerifyAsync(
                    snapshot => snapshot.AlwaysOnUsb,
                    desiredUsb,
                    "always_on_usb_readback_mismatch",
                    cancellationToken)
                    .ConfigureAwait(false);
            case HardwareWriteKind.TouchpadLock:
                _ = ParseToggle(expected);
                ToggleState desiredTouchpad = ParseToggle(desired);
                ToggleState currentTouchpad = RequireCurrent(
                    before.TouchpadLock,
                    "touchpad_expected_mismatch");
                if (currentTouchpad == desiredTouchpad)
                    return before;

                await _writer
                    .WriteTouchpadLockAsync(desiredTouchpad, cancellationToken)
                    .ConfigureAwait(false);
                return await VerifyAsync(
                    snapshot => snapshot.TouchpadLock,
                    desiredTouchpad,
                    "touchpad_readback_mismatch",
                    cancellationToken)
                    .ConfigureAwait(false);
            case HardwareWriteKind.WinKeyLock:
                _ = ParseToggle(expected);
                ToggleState desiredWinKey = ParseToggle(desired);
                ToggleState currentWinKey = RequireCurrent(
                    before.WinKeyLock,
                    "win_key_expected_mismatch");
                if (currentWinKey == desiredWinKey)
                    return before;

                await _writer
                    .WriteWinKeyLockAsync(desiredWinKey, cancellationToken)
                    .ConfigureAwait(false);
                return await VerifyAsync(
                    snapshot => snapshot.WinKeyLock,
                    desiredWinKey,
                    "win_key_readback_mismatch",
                    cancellationToken)
                    .ConfigureAwait(false);
            case HardwareWriteKind.FourZoneLighting:
                _ = HardwareStateTokens.ParseLighting(expected);
                FourZoneLightingState desiredLighting = HardwareStateTokens.ParseLighting(desired);
                await _writer
                    .WriteFourZoneLightingAsync(desiredLighting, cancellationToken)
                    .ConfigureAwait(false);
                return await VerifyLightingAsync(desiredLighting, cancellationToken)
                    .ConfigureAwait(false);
            case HardwareWriteKind.FanTable:
                HardwareStateSnapshot fanBefore = await CaptureAsync(
                        cancellationToken,
                        includeFanTable: true)
                    .ConfigureAwait(false);
                _ = HardwareStateTokens.ParseFanTable(expected);
                FanTableSnapshot desiredFan = HardwareStateTokens.ParseFanTable(desired);
                RequireCurrent(fanBefore.FanTable, "fan_table_expected_mismatch");
                await _writer
                    .WriteFanTableAsync(desiredFan, cancellationToken)
                    .ConfigureAwait(false);
                return await VerifyFanTableAsync(desiredFan, cancellationToken)
                    .ConfigureAwait(false);
            case HardwareWriteKind.SpectrumKeyboard:
                _ = ParseSpectrum(expected);
                SpectrumBrightness desiredSpectrum = ParseSpectrum(desired);
                SpectrumBrightness currentSpectrum = RequireCurrent(
                    before.SpectrumKeyboard,
                    "spectrum_expected_mismatch");
                if (currentSpectrum == desiredSpectrum)
                    return before;

                await _writer
                    .WriteSpectrumKeyboardAsync(desiredSpectrum, cancellationToken)
                    .ConfigureAwait(false);
                return await VerifyAsync(
                    snapshot => snapshot.SpectrumKeyboard,
                    desiredSpectrum,
                    "spectrum_readback_mismatch",
                    cancellationToken)
                    .ConfigureAwait(false);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private async ValueTask<HardwareStateSnapshot> CaptureAsync(
        CancellationToken cancellationToken,
        bool includeFanTable = false) =>
        await new HardwareStateService(_createReader())
            .CaptureAsync(cancellationToken, includeFanTable)
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

    private static T RequireCurrent<T>(
        HardwareReadResult<T> result,
        string errorCode)
        where T : struct
    {
        if (result.Status != HardwareReadStatus.Success || !result.Value.HasValue)
            throw new HardwareWriteException(errorCode, HardwareWriteStatus.Conflict);

        return result.Value.Value;
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

    private static AlwaysOnUsbState ParseAlwaysOnUsb(string value) =>
        ParseEnum<AlwaysOnUsbState>(value, "always_on_usb_value_invalid");

    private static SpectrumBrightness ParseSpectrum(string value) =>
        ParseEnum<SpectrumBrightness>(value, "spectrum_value_invalid");

    private async ValueTask<HardwareStateSnapshot> VerifyLightingAsync(
        FourZoneLightingState desired,
        CancellationToken cancellationToken)
    {
        HardwareStateSnapshot after = await CaptureAsync(cancellationToken).ConfigureAwait(false);
        if (after.FourZoneLighting.Status == HardwareReadStatus.Success &&
            after.FourZoneLighting.Value.HasValue &&
            after.FourZoneLighting.Value.Value.Equals(desired))
        {
            return after;
        }

        if (after.FourZoneKeyboard is { Status: HardwareReadStatus.Success, Value: { } brightness } &&
            (brightness == FourZoneKeyboardMode.Unknown || brightness == desired.Brightness))
        {
            return after;
        }

        throw new HardwareWriteException(
            "lighting_readback_mismatch",
            HardwareWriteStatus.Unverified);
    }

    private async ValueTask<HardwareStateSnapshot> VerifyFanTableAsync(
        FanTableSnapshot desired,
        CancellationToken cancellationToken)
    {
        HardwareStateSnapshot after = await CaptureAsync(
                cancellationToken,
                includeFanTable: true)
            .ConfigureAwait(false);
        HardwareReadResult<FanTableSnapshot> result = after.FanTable;
        if (result.Status != HardwareReadStatus.Success ||
            !result.Value.HasValue ||
            result.Value.Value.PointCount != desired.PointCount)
        {
            throw new HardwareWriteException(
                "fan_table_readback_mismatch",
                HardwareWriteStatus.Unverified);
        }

        FanTableSnapshot actual = result.Value.Value;
        for (int index = 0; index < desired.PointCount; index++)
        {
            if (actual.Points[index] != desired.Points[index])
            {
                throw new HardwareWriteException(
                    "fan_table_readback_mismatch",
                    HardwareWriteStatus.Unverified);
            }
        }

        return after;
    }

    private static void RequireKeyboardPresence(
        HardwareReadResult<FourZoneKeyboardMode> result)
    {
        if (result.Status != HardwareReadStatus.Success || !result.Value.HasValue)
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
