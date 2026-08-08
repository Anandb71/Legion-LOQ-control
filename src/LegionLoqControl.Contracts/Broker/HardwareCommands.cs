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
