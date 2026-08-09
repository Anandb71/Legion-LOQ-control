using LegionLoqControl.Domain.Controls;

namespace LegionLoqControl.Domain.Profiles;

public readonly record struct ProfileId
{
    public ProfileId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("A profile ID cannot be empty.", nameof(value));

        Value = value;
    }

    public Guid Value { get; }

    public static ProfileId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record HardwareProfileTargets
{
    public HardwareProfileTargets(
        BatteryChargeMode? batteryChargeMode = null,
        ThermalMode? thermalMode = null)
    {
        if (batteryChargeMode.HasValue && !Enum.IsDefined(batteryChargeMode.Value))
            throw new ArgumentOutOfRangeException(nameof(batteryChargeMode));
        if (thermalMode.HasValue && !Enum.IsDefined(thermalMode.Value))
            throw new ArgumentOutOfRangeException(nameof(thermalMode));
        if (!batteryChargeMode.HasValue && !thermalMode.HasValue)
            throw new ArgumentException("A profile must contain at least one hardware target.");

        BatteryChargeMode = batteryChargeMode;
        ThermalMode = thermalMode;
    }

    public BatteryChargeMode? BatteryChargeMode { get; }

    public ThermalMode? ThermalMode { get; }
}

public sealed record HardwareProfile
{
    public const int MaximumNameLength = 64;

    public HardwareProfile(
        ProfileId id,
        string name,
        HardwareProfileTargets targets)
    {
        if (id.Value == Guid.Empty)
            throw new ArgumentException("A profile ID cannot be empty.", nameof(id));

        ArgumentNullException.ThrowIfNull(targets);

        string normalizedName = NormalizeName(name);
        Id = id;
        Name = normalizedName;
        Targets = targets;
    }

    public ProfileId Id { get; }

    public string Name { get; }

    public HardwareProfileTargets Targets { get; }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string normalized = name.Trim();
        if (normalized.Length > MaximumNameLength)
            throw new ArgumentOutOfRangeException(nameof(name), $"A profile name cannot exceed {MaximumNameLength} characters.");
        if (normalized.Any(char.IsControl))
            throw new ArgumentException("A profile name cannot contain control characters.", nameof(name));

        return normalized;
    }
}

