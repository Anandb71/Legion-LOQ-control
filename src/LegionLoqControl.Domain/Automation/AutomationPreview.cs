using LegionLoqControl.Domain.Profiles;

namespace LegionLoqControl.Domain.Automation;

public enum AutomationPreviewStatus
{
    WouldSelect = 0,
    NoEnabledRules = 1,
    PowerSourceUnavailable = 2,
    PowerSourceStale = 3,
    NoMatch = 4,
    Ambiguous = 5,
    ProfileMissing = 6,
}
public sealed record AutomationPreview
{
    private AutomationPreview(
        AutomationPreviewStatus status,
        DateTimeOffset evaluatedAt,
        PowerSourceKind? powerSource,
        AutomationRule? selectedRule,
        HardwareProfile? selectedProfile,
        string? reasonCode)
    {
        Status = status;
        EvaluatedAt = evaluatedAt;
        PowerSource = powerSource;
        SelectedRule = selectedRule;
        SelectedProfile = selectedProfile;
        ReasonCode = reasonCode;
    }

    public AutomationPreviewStatus Status { get; }

    public DateTimeOffset EvaluatedAt { get; }

    public PowerSourceKind? PowerSource { get; }

    public AutomationRule? SelectedRule { get; }

    public HardwareProfile? SelectedProfile { get; }

    public string? ReasonCode { get; }

    public static AutomationPreview WouldSelect(
        DateTimeOffset evaluatedAt,
        PowerSourceKind powerSource,
        AutomationRule rule,
        HardwareProfile profile)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(profile);
        if (rule.ProfileId != profile.Id)
            throw new ArgumentException("The selected rule and profile IDs do not match.");

        return new AutomationPreview(
            AutomationPreviewStatus.WouldSelect,
            evaluatedAt,
            powerSource,
            rule,
            profile,
            reasonCode: null);
    }

    public static AutomationPreview Blocked(
        AutomationPreviewStatus status,
        DateTimeOffset evaluatedAt,
        PowerSourceKind? powerSource,
        string reasonCode)
    {
        if (status == AutomationPreviewStatus.WouldSelect || !Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));

        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return new AutomationPreview(
            status,
            evaluatedAt,
            powerSource,
            selectedRule: null,
            selectedProfile: null,
            reasonCode.Trim());
    }
}
