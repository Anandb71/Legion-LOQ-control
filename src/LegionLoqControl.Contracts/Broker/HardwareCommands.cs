using LegionLoqControl.Domain.Controls;

namespace LegionLoqControl.Contracts.Broker;

public interface IHardwareCommand
{
    CommandId CommandId { get; }
}

public sealed record SetBatteryChargeModeCommand(
    CommandId CommandId,
    BatteryChargeMode Expected,
    BatteryChargeMode Desired) : IHardwareCommand;

public sealed record SetThermalModeCommand(
    CommandId CommandId,
    ThermalMode Expected,
    ThermalMode Desired) : IHardwareCommand;

public sealed record SetFanModeCommand(
    CommandId CommandId,
    FanMode Expected,
    FanMode Desired) : IHardwareCommand;

public sealed record SetKeyboardBrightnessCommand(
    CommandId CommandId,
    KeyboardBrightness Expected,
    KeyboardBrightness Desired) : IHardwareCommand;

public sealed record SetDisplayOverdriveCommand(
    CommandId CommandId,
    ToggleState Expected,
    ToggleState Desired) : IHardwareCommand;

public sealed record SetIntegratedGpuModeCommand(
    CommandId CommandId,
    IntegratedGpuMode Expected,
    IntegratedGpuMode Desired) : IHardwareCommand;

public enum HardwareWriteTarget
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

public enum BrokerCommandStatus
{
    Succeeded = 0,
    Unsupported = 1,
    InvalidRequest = 2,
    Conflict = 3,
    Busy = 4,
    Unverified = 5,
    Failed = 6,
}

public sealed record BrokerCommandResult(
    CommandId CommandId,
    BrokerCommandStatus Status,
    string? ErrorCode = null);
