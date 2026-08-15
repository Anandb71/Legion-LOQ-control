namespace LegionLoqControl.Domain.Controls;

public readonly record struct FanTablePoint(byte Speed, byte Sensor);

public readonly record struct FanTableSnapshot(
    byte FanId,
    byte SensorId,
    FanTablePoint[] Points)
{
    public const int MaximumPoints = 10;
    public const byte DefaultFanId = 0;
    public const byte DefaultSensorId = 0;

    public int PointCount => Points?.Length ?? 0;
}
