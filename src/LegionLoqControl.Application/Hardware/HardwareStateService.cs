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

        return new HardwareStateSnapshot(
            _timeProvider.GetUtcNow(),
            battery,
            thermal,
            overdrive,
            integratedGpu);
    }
}
