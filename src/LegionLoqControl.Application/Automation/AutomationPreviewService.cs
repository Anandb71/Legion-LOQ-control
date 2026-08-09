using LegionLoqControl.Domain.Automation;
using LegionLoqControl.Domain.Profiles;

namespace LegionLoqControl.Application.Automation;

public sealed class AutomationPreviewService
{
    public static readonly TimeSpan DefaultMaximumPowerSourceAge =
        TimeSpan.FromMinutes(1);

    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _maximumPowerSourceAge;

    public AutomationPreviewService(
        TimeProvider? timeProvider = null,
        TimeSpan? maximumPowerSourceAge = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _maximumPowerSourceAge =
            maximumPowerSourceAge ?? DefaultMaximumPowerSourceAge;
        if (_maximumPowerSourceAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maximumPowerSourceAge));
    }

    public AutomationPreview Create(
        IReadOnlyList<AutomationRule> rules,
        IReadOnlyList<HardwareProfile> profiles,
        PowerSourceSnapshot? powerSourceSnapshot)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(profiles);
        if (rules.Any(static rule => rule is null))
            throw new ArgumentException("Automation rules cannot contain null entries.", nameof(rules));
        if (profiles.Any(static profile => profile is null))
            throw new ArgumentException("Profiles cannot contain null entries.", nameof(profiles));

        DateTimeOffset evaluatedAt = _timeProvider.GetUtcNow();
        if (powerSourceSnapshot is null)
        {
            return AutomationPreview.Blocked(
                AutomationPreviewStatus.PowerSourceUnavailable,
                evaluatedAt,
                powerSource: null,
                "power_source_not_read");
        }

        if (!powerSourceSnapshot.PowerSource.HasValue)
        {
            return AutomationPreview.Blocked(
                AutomationPreviewStatus.PowerSourceUnavailable,
                evaluatedAt,
                powerSource: null,
                powerSourceSnapshot.PowerSource.ErrorCode
                    ?? "power_source_unavailable");
        }

        PowerSourceKind powerSource =
            powerSourceSnapshot.PowerSource.Value!.Value;
        string? staleReason = GetStaleReason(
            powerSourceSnapshot.ObservedAt,
            evaluatedAt);
        if (staleReason is not null)
        {
            return AutomationPreview.Blocked(
                AutomationPreviewStatus.PowerSourceStale,
                evaluatedAt,
                powerSource,
                staleReason);
        }

        AutomationRule[] enabled = rules
            .Where(static rule => rule.IsEnabled)
            .ToArray();
        if (enabled.Length == 0)
        {
            return AutomationPreview.Blocked(
                AutomationPreviewStatus.NoEnabledRules,
                evaluatedAt,
                powerSource,
                "automation_no_enabled_rules");
        }

        if (enabled
            .GroupBy(static rule => rule.Id)
            .Any(static group => group.Count() > 1))
        {
            return AutomationPreview.Blocked(
                AutomationPreviewStatus.Ambiguous,
                evaluatedAt,
                powerSource,
                "automation_rule_id_duplicated");
        }

        AutomationRule[] matching = enabled
            .Where(rule => rule.RequiredPowerSource == powerSource)
            .ToArray();
        if (matching.Length == 0)
        {
            return AutomationPreview.Blocked(
                AutomationPreviewStatus.NoMatch,
                evaluatedAt,
                powerSource,
                "automation_no_matching_rule");
        }

        int highestPriority = matching.Max(static rule => rule.Priority);
        AutomationRule[] highest = matching
            .Where(rule => rule.Priority == highestPriority)
            .ToArray();
        if (highest.Length != 1)
        {
            return AutomationPreview.Blocked(
                AutomationPreviewStatus.Ambiguous,
                evaluatedAt,
                powerSource,
                "automation_priority_ambiguous");
        }

        AutomationRule selectedRule = highest[0];
        HardwareProfile[] selectedProfiles = profiles
            .Where(profile => profile.Id == selectedRule.ProfileId)
            .ToArray();
        if (selectedProfiles.Length == 0)
        {
            return AutomationPreview.Blocked(
                AutomationPreviewStatus.ProfileMissing,
                evaluatedAt,
                powerSource,
                "automation_profile_missing");
        }

        if (selectedProfiles.Length != 1)
        {
            return AutomationPreview.Blocked(
                AutomationPreviewStatus.ProfileMissing,
                evaluatedAt,
                powerSource,
                "automation_profile_ambiguous");
        }

        return AutomationPreview.WouldSelect(
            evaluatedAt,
            powerSource,
            selectedRule,
            selectedProfiles[0]);
    }

    private string? GetStaleReason(
        DateTimeOffset observedAt,
        DateTimeOffset evaluatedAt)
    {
        if (observedAt > evaluatedAt)
            return "power_source_timestamp_invalid";

        return evaluatedAt - observedAt > _maximumPowerSourceAge
            ? "power_source_stale"
            : null;
    }
}
