namespace LegionLoqControl.Domain.Controls;

public enum BatteryChargeMode
{
    Normal = 0,
    Conservation = 1,
    RapidCharge = 2,
}

public enum ThermalMode
{
    Quiet = 0,
    Balanced = 1,
    Performance = 2,
    Extreme = 3,
    Custom = 4,
}

public enum FanMode
{
    Automatic = 0,
    FullSpeed = 1,
}

public enum FourZoneKeyboardMode
{
    Unknown = 0,
    Off = 1,
    Low = 2,
    High = 3,
}

public readonly record struct KeyboardBrightness
{
    public const byte Minimum = 0;
    public const byte Maximum = 9;

    public KeyboardBrightness(byte level)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, Maximum);
        Level = level;
    }

    public byte Level { get; }
}

public enum ToggleState
{
    Disabled = 0,
    Enabled = 1,
}

public enum IntegratedGpuMode
{
    Default = 0,
    IntegratedOnly = 1,
    Automatic = 2,
}
