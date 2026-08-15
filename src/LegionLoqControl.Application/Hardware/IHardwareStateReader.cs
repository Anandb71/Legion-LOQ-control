using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Application.Hardware;

public interface IHardwareStateReader
{
    ValueTask<HardwareReadResult<BatteryChargeMode>> ReadBatteryChargeModeAsync(
        CancellationToken cancellationToken);

    ValueTask<HardwareReadResult<ThermalMode>> ReadThermalModeAsync(
        CancellationToken cancellationToken);

    ValueTask<HardwareReadResult<ToggleState>> ReadDisplayOverdriveAsync(
        CancellationToken cancellationToken);

    ValueTask<HardwareReadResult<IntegratedGpuMode>> ReadIntegratedGpuModeAsync(
        CancellationToken cancellationToken);

    ValueTask<HardwareReadResult<FourZoneKeyboardMode>> ReadFourZoneKeyboardAsync(
        CancellationToken cancellationToken);

    ValueTask<HardwareReadResult<FanTableSnapshot>> ReadFanTableAsync(
        CancellationToken cancellationToken);

    ValueTask<HardwareReadResult<ToggleState>> ReadOvernightChargeAsync(
        CancellationToken cancellationToken) =>
        Unavailable<ToggleState>("overnight_not_implemented");

    ValueTask<HardwareReadResult<ToggleState>> ReadFnLockAsync(
        CancellationToken cancellationToken) =>
        Unavailable<ToggleState>("fn_lock_not_implemented");

    ValueTask<HardwareReadResult<AlwaysOnUsbState>> ReadAlwaysOnUsbAsync(
        CancellationToken cancellationToken) =>
        Unavailable<AlwaysOnUsbState>("always_on_usb_not_implemented");

    ValueTask<HardwareReadResult<ToggleState>> ReadTouchpadLockAsync(
        CancellationToken cancellationToken) =>
        Unavailable<ToggleState>("touchpad_lock_not_implemented");

    ValueTask<HardwareReadResult<ToggleState>> ReadWinKeyLockAsync(
        CancellationToken cancellationToken) =>
        Unavailable<ToggleState>("win_key_lock_not_implemented");

    ValueTask<HardwareReadResult<FourZoneLightingState>> ReadFourZoneLightingAsync(
        CancellationToken cancellationToken) =>
        Unavailable<FourZoneLightingState>("four_zone_lighting_not_implemented");

    ValueTask<HardwareReadResult<SpectrumBrightness>> ReadSpectrumKeyboardAsync(
        CancellationToken cancellationToken) =>
        Unavailable<SpectrumBrightness>("spectrum_not_implemented");

    private static ValueTask<HardwareReadResult<T>> Unavailable<T>(string errorCode)
        where T : struct =>
        ValueTask.FromResult(HardwareReadResult<T>.Failure(
            HardwareReadStatus.Unavailable,
            errorCode));
}
