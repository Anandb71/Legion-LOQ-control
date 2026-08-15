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

public enum AlwaysOnUsbState
{
    Off = 0,
    OnWhenSleeping = 1,
    OnAlways = 2,
}

public enum FourZoneEffect
{
    Off = 0,
    Static = 1,
    Breath = 3,
    Wave = 4,
    Smooth = 6,
}

public readonly record struct RgbColor(byte Red, byte Green, byte Blue)
{
    public static RgbColor White { get; } = new(255, 255, 255);

    public string ToHex() => $"{Red:X2}{Green:X2}{Blue:X2}";

    public static bool TryParseHex(string? value, out RgbColor color)
    {
        color = default;
        if (value is not { Length: 6 })
            return false;
        if (!byte.TryParse(value.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte red) ||
            !byte.TryParse(value.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte green) ||
            !byte.TryParse(value.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte blue))
        {
            return false;
        }

        color = new RgbColor(red, green, blue);
        return true;
    }
}

public readonly record struct FourZoneLightingState(
    FourZoneEffect Effect,
    FourZoneKeyboardMode Brightness,
    byte Speed,
    bool DivideArea,
    RgbColor Zone1,
    RgbColor Zone2,
    RgbColor Zone3,
    RgbColor Zone4)
{
    public const byte MaximumSpeed = 4;

    public static FourZoneLightingState Default { get; } = new(
        FourZoneEffect.Static,
        FourZoneKeyboardMode.High,
        Speed: 1,
        DivideArea: false,
        RgbColor.White,
        RgbColor.White,
        RgbColor.White,
        RgbColor.White);
}

public enum SpectrumBrightness
{
    Off = 0,
    Low = 1,
    Medium = 2,
    High = 3,
}
