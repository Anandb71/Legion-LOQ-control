using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Domain.Diagnostics;

public sealed record HardwareStateSnapshot(
    DateTimeOffset ObservedAt,
    HardwareReadResult<BatteryChargeMode> BatteryChargeMode,
    HardwareReadResult<ThermalMode> ThermalMode,
    HardwareReadResult<ToggleState> DisplayOverdrive,
    HardwareReadResult<IntegratedGpuMode> IntegratedGpuMode,
    HardwareReadResult<FourZoneKeyboardMode> FourZoneKeyboard,
    HardwareReadResult<FanTableSnapshot> FanTable)
{
    public HardwareReadResult<ToggleState> OvernightCharge { get; init; } =
        NotCaptured<ToggleState>("overnight_not_captured");

    public HardwareReadResult<ToggleState> FnLock { get; init; } =
        NotCaptured<ToggleState>("fn_lock_not_captured");

    public HardwareReadResult<AlwaysOnUsbState> AlwaysOnUsb { get; init; } =
        NotCaptured<AlwaysOnUsbState>("always_on_usb_not_captured");

    public HardwareReadResult<ToggleState> TouchpadLock { get; init; } =
        NotCaptured<ToggleState>("touchpad_lock_not_captured");

    public HardwareReadResult<ToggleState> WinKeyLock { get; init; } =
        NotCaptured<ToggleState>("win_key_lock_not_captured");

    public HardwareReadResult<FourZoneLightingState> FourZoneLighting { get; init; } =
        NotCaptured<FourZoneLightingState>("four_zone_lighting_not_captured");

    public HardwareReadResult<SpectrumBrightness> SpectrumKeyboard { get; init; } =
        NotCaptured<SpectrumBrightness>("spectrum_not_captured");

    public static HardwareReadResult<T> NotCaptured<T>(string errorCode)
        where T : struct =>
        HardwareReadResult<T>.Failure(HardwareReadStatus.Unavailable, errorCode);
}
