using LegionLoqControl.Domain.Profiles;

namespace LegionLoqControl.Domain.Automation;

public readonly record struct AutomationRuleId
{
    public AutomationRuleId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("An automation rule ID cannot be empty.", nameof(value));

        Value = value;
    }

    public Guid Value { get; }

    public static AutomationRuleId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
public sealed record AutomationRule
{
    public const int MaximumNameLength = 64;
    public const int MinimumPriority = 0;
    public const int MaximumPriority = 1000;

    public AutomationRule(
        AutomationRuleId id,
        string name,
        ProfileId profileId,
        PowerSourceKind requiredPowerSource,
        int priority,
        bool isEnabled = true)
    {
        if (id.Value == Guid.Empty)
            throw new ArgumentException("An automation rule ID cannot be empty.", nameof(id));
        if (profileId.Value == Guid.Empty)
            throw new ArgumentException("A target profile ID cannot be empty.", nameof(profileId));
        if (!Enum.IsDefined(requiredPowerSource))
            throw new ArgumentOutOfRangeException(nameof(requiredPowerSource));
        if (priority is < MinimumPriority or > MaximumPriority)
            throw new ArgumentOutOfRangeException(nameof(priority));

        Id = id;
        Name = NormalizeName(name);
        ProfileId = profileId;
        RequiredPowerSource = requiredPowerSource;
        Priority = priority;
        IsEnabled = isEnabled;
    }

    public AutomationRuleId Id { get; }

    public string Name { get; }

    public ProfileId ProfileId { get; }

    public PowerSourceKind RequiredPowerSource { get; }

    public int Priority { get; }

    public bool IsEnabled { get; }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string normalized = name.Trim();
        if (normalized.Length > MaximumNameLength)
            throw new ArgumentOutOfRangeException(nameof(name), $"A rule name cannot exceed {MaximumNameLength} characters.");
        if (normalized.Any(char.IsControl))
            throw new ArgumentException("A rule name cannot contain control characters.", nameof(name));

        return normalized;
    }
}
