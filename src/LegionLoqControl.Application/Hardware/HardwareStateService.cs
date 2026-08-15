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
        CancellationToken cancellationToken = default)
    {
        HardwareReadResult<BatteryChargeMode> battery = await _reader
            .ReadBatteryChargeModeAsync(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        ValueTask<HardwareReadResult<ThermalMode>> thermalTask = _reader
            .ReadThermalModeAsync(cancellationToken);
        ValueTask<HardwareReadResult<FanTableSnapshot>> fanTask = _reader
            .ReadFanTableAsync(cancellationToken);

        HardwareReadResult<ThermalMode> thermal = await thermalTask.ConfigureAwait(false);
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

        HardwareReadResult<FanTableSnapshot> fanTable = await fanTask.ConfigureAwait(false);

        return new HardwareStateSnapshot(
            _timeProvider.GetUtcNow(),
            battery,
            thermal,
            overdrive,
            integratedGpu,
            keyboard,
            fanTable);
    }
}
