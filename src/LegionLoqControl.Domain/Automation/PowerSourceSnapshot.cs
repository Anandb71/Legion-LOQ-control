using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Domain.Automation;

public enum PowerSourceKind
{
    Ac = 0,
    Battery = 1,
}

public readonly record struct SystemPowerTelemetry(
    HardwareReadResult<PowerSourceKind> Source,
    int? BatteryPercent,
    bool Charging)
{
    public static SystemPowerTelemetry Unavailable { get; } = new(
        HardwareReadResult<PowerSourceKind>.Failure(
            HardwareReadStatus.Failed,
            "power_status_api_failed"),
        null,
        false);
}

public sealed record PowerSourceSnapshot
{
    public PowerSourceSnapshot(
        DateTimeOffset observedAt,
        HardwareReadResult<PowerSourceKind> powerSource)
    {
        ObservedAt = observedAt;
        PowerSource = powerSource ?? throw new ArgumentNullException(nameof(powerSource));
    }

    public DateTimeOffset ObservedAt { get; }

    public HardwareReadResult<PowerSourceKind> PowerSource { get; }
}

