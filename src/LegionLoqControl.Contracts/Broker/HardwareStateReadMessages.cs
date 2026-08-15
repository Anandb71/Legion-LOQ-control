using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Contracts.Broker;

public enum BrokerReadStatus
{
    Succeeded = 0,
    InvalidRequest = 1,
    VersionMismatch = 2,
    Unauthorized = 3,
    Failed = 4,
}

public sealed record HardwareStateReadRequest(
    ushort ProtocolMajorVersion,
    Guid RequestId,
    string Nonce,
    int ClientProcessId,
    bool IncludeFanTable = true);

public sealed record HardwareStateReadResponse(
    ushort ProtocolMajorVersion,
    Guid RequestId,
    BrokerReadStatus Status,
    HardwareStateReadPayload? Snapshot,
    string? ErrorCode);

public sealed record HardwareStateReadPayload(
    DateTimeOffset ObservedAt,
    HardwareReadValue<BatteryChargeMode> BatteryChargeMode,
    HardwareReadValue<ThermalMode> ThermalMode,
    HardwareReadValue<ToggleState> DisplayOverdrive,
    HardwareReadValue<IntegratedGpuMode> IntegratedGpuMode,
    HardwareReadValue<FourZoneKeyboardMode> FourZoneKeyboard,
    HardwareReadValue<FanTableSnapshot> FanTable)
{
    public static HardwareStateReadPayload FromSnapshot(HardwareStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new HardwareStateReadPayload(
            snapshot.ObservedAt,
            HardwareReadValue<BatteryChargeMode>.FromResult(snapshot.BatteryChargeMode),
            HardwareReadValue<ThermalMode>.FromResult(snapshot.ThermalMode),
            HardwareReadValue<ToggleState>.FromResult(snapshot.DisplayOverdrive),
            HardwareReadValue<IntegratedGpuMode>.FromResult(snapshot.IntegratedGpuMode),
            HardwareReadValue<FourZoneKeyboardMode>.FromResult(snapshot.FourZoneKeyboard),
            HardwareReadValue<FanTableSnapshot>.FromResult(snapshot.FanTable))
        {
            OvernightCharge = HardwareReadValue<ToggleState>.FromResult(snapshot.OvernightCharge),
            FnLock = HardwareReadValue<ToggleState>.FromResult(snapshot.FnLock),
            AlwaysOnUsb = HardwareReadValue<AlwaysOnUsbState>.FromResult(snapshot.AlwaysOnUsb),
            TouchpadLock = HardwareReadValue<ToggleState>.FromResult(snapshot.TouchpadLock),
            WinKeyLock = HardwareReadValue<ToggleState>.FromResult(snapshot.WinKeyLock),
            FourZoneLighting = HardwareReadValue<FourZoneLightingState>.FromResult(
                snapshot.FourZoneLighting),
            SpectrumKeyboard = HardwareReadValue<SpectrumBrightness>.FromResult(
                snapshot.SpectrumKeyboard),
        };
    }

    public HardwareReadValue<ToggleState>? OvernightCharge { get; init; }

    public HardwareReadValue<ToggleState>? FnLock { get; init; }

    public HardwareReadValue<AlwaysOnUsbState>? AlwaysOnUsb { get; init; }

    public HardwareReadValue<ToggleState>? TouchpadLock { get; init; }

    public HardwareReadValue<ToggleState>? WinKeyLock { get; init; }

    public HardwareReadValue<FourZoneLightingState>? FourZoneLighting { get; init; }

    public HardwareReadValue<SpectrumBrightness>? SpectrumKeyboard { get; init; }

    public HardwareStateSnapshot ToSnapshot()
    {
        if (BatteryChargeMode is null ||
            ThermalMode is null ||
            DisplayOverdrive is null ||
            IntegratedGpuMode is null ||
            FourZoneKeyboard is null ||
            FanTable is null)
        {
            throw new InvalidDataException("The hardware state payload is incomplete.");
        }

        HardwareReadResult<FanTableSnapshot> fanTable = FanTable.ToResult();
        if (fanTable.Status == HardwareReadStatus.Success &&
            (!fanTable.Value.HasValue ||
             fanTable.Value.Value.PointCount is < 1 or > FanTableSnapshot.MaximumPoints ||
             fanTable.Value.Value.Points is null))
        {
            throw new InvalidDataException("The hardware state payload is incomplete.");
        }

        return new HardwareStateSnapshot(
            ObservedAt,
            BatteryChargeMode.ToResult(),
            ThermalMode.ToResult(),
            DisplayOverdrive.ToResult(),
            IntegratedGpuMode.ToResult(),
            FourZoneKeyboard.ToResult(),
            fanTable)
        {
            OvernightCharge = OvernightCharge?.ToResult()
                ?? HardwareStateSnapshot.NotCaptured<ToggleState>("overnight_not_captured"),
            FnLock = FnLock?.ToResult()
                ?? HardwareStateSnapshot.NotCaptured<ToggleState>("fn_lock_not_captured"),
            AlwaysOnUsb = AlwaysOnUsb?.ToResult()
                ?? HardwareStateSnapshot.NotCaptured<AlwaysOnUsbState>("always_on_usb_not_captured"),
            TouchpadLock = TouchpadLock?.ToResult()
                ?? HardwareStateSnapshot.NotCaptured<ToggleState>("touchpad_lock_not_captured"),
            WinKeyLock = WinKeyLock?.ToResult()
                ?? HardwareStateSnapshot.NotCaptured<ToggleState>("win_key_lock_not_captured"),
            FourZoneLighting = FourZoneLighting?.ToResult()
                ?? HardwareStateSnapshot.NotCaptured<FourZoneLightingState>(
                    "four_zone_lighting_not_captured"),
            SpectrumKeyboard = SpectrumKeyboard?.ToResult()
                ?? HardwareStateSnapshot.NotCaptured<SpectrumBrightness>("spectrum_not_captured"),
        };
    }
}

public sealed record HardwareReadValue<T>(
    HardwareReadStatus Status,
    T? Value,
    string? ErrorCode)
    where T : struct
{
    public static HardwareReadValue<T> FromResult(HardwareReadResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new HardwareReadValue<T>(result.Status, result.Value, result.ErrorCode);
    }

    public HardwareReadResult<T> ToResult()
    {
        if (!Enum.IsDefined(Status))
            throw new InvalidDataException("The hardware read status is invalid.");

        if (Status == HardwareReadStatus.Success)
        {
            if (!Value.HasValue || ErrorCode is not null)
                throw new InvalidDataException("A successful hardware read has an invalid shape.");
            return HardwareReadResult<T>.Success(Value.Value);
        }

        if (Value.HasValue || !IsValidErrorCode(ErrorCode))
            throw new InvalidDataException("A failed hardware read has an invalid shape.");
        return HardwareReadResult<T>.Failure(Status, ErrorCode!);
    }

    private static bool IsValidErrorCode(string? value) =>
        value is { Length: > 0 and <= 64 } &&
        value.All(static character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
}
