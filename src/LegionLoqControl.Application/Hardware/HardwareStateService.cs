using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Application.Hardware;

public sealed class HardwareStateService
{
    private readonly IHardwareStateReader _reader;
    private readonly TimeProvider _timeProvider;

    public HardwareStateService(IHardwareStateReader reader, TimeProvider? timeProvider = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<HardwareStateSnapshot> CaptureAsync(
        CancellationToken cancellationToken = default,
        bool includeFanTable = true)
    {
        HardwareReadResult<BatteryChargeMode> battery = await _reader
            .ReadBatteryChargeModeAsync(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        HardwareReadResult<ThermalMode> thermal = await _reader
            .ReadThermalModeAsync(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        HardwareReadResult<ToggleState> overdrive = await _reader
            .ReadDisplayOverdriveAsync(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        HardwareReadResult<IntegratedGpuMode> integratedGpu = await _reader
            .ReadIntegratedGpuModeAsync(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        HardwareReadResult<FourZoneKeyboardMode> keyboard = await _reader
            .ReadFourZoneKeyboardAsync(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        HardwareReadResult<FanTableSnapshot> fanTable = includeFanTable
            ? await _reader.ReadFanTableAsync(cancellationToken).ConfigureAwait(false)
            : HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.Unavailable,
                "fan_table_not_requested");

        HardwareReadResult<ToggleState> overnight = await _reader
            .ReadOvernightChargeAsync(cancellationToken)
            .ConfigureAwait(false);
        HardwareReadResult<ToggleState> fnLock = await _reader
            .ReadFnLockAsync(cancellationToken)
            .ConfigureAwait(false);
        HardwareReadResult<AlwaysOnUsbState> alwaysOnUsb = await _reader
            .ReadAlwaysOnUsbAsync(cancellationToken)
            .ConfigureAwait(false);
        HardwareReadResult<ToggleState> touchpad = await _reader
            .ReadTouchpadLockAsync(cancellationToken)
            .ConfigureAwait(false);
        HardwareReadResult<ToggleState> winKey = await _reader
            .ReadWinKeyLockAsync(cancellationToken)
            .ConfigureAwait(false);
        HardwareReadResult<FourZoneLightingState> lighting = await _reader
            .ReadFourZoneLightingAsync(cancellationToken)
            .ConfigureAwait(false);
        HardwareReadResult<SpectrumBrightness> spectrum = await _reader
            .ReadSpectrumKeyboardAsync(cancellationToken)
            .ConfigureAwait(false);

        return new HardwareStateSnapshot(
            _timeProvider.GetUtcNow(),
            battery,
            thermal,
            overdrive,
            integratedGpu,
            keyboard,
            fanTable)
        {
            OvernightCharge = overnight,
            FnLock = fnLock,
            AlwaysOnUsb = alwaysOnUsb,
            TouchpadLock = touchpad,
            WinKeyLock = winKey,
            FourZoneLighting = lighting,
            SpectrumKeyboard = spectrum,
        };
    }
}
