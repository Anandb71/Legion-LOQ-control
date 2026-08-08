using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Domain.Diagnostics;

public sealed record HardwareStateSnapshot(
    DateTimeOffset ObservedAt,
    HardwareReadResult<BatteryChargeMode> BatteryChargeMode,
    HardwareReadResult<ThermalMode> ThermalMode,
    HardwareReadResult<ToggleState> DisplayOverdrive,
    HardwareReadResult<IntegratedGpuMode> IntegratedGpuMode);
