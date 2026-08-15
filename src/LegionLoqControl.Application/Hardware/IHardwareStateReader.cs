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
}
