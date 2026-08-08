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
    Custom = 3,
}

public enum FanMode
{
    Automatic = 0,
    FullSpeed = 1,
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
