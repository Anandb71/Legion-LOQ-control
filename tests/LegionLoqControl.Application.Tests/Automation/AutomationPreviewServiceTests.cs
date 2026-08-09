using LegionLoqControl.Application.Automation;
using LegionLoqControl.Domain.Automation;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Profiles;
using LegionLoqControl.Domain.Results;
using Xunit;

namespace LegionLoqControl.Application.Tests.Automation;

public sealed class AutomationPreviewServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Highest_priority_matching_rule_selects_its_profile()
    {
        HardwareProfile lowProfile = CreateProfile("Low");
        HardwareProfile highProfile = CreateProfile("High");
        AutomationRule[] rules =
        [
            CreateRule("Low AC", lowProfile.Id, PowerSourceKind.Ac, priority: 10),
            CreateRule("High AC", highProfile.Id, PowerSourceKind.Ac, priority: 20),
            CreateRule("Battery", lowProfile.Id, PowerSourceKind.Battery, priority: 100),
        ];

        AutomationPreview preview = CreateService().Create(
            rules,
            [lowProfile, highProfile],
            CreatePowerSource(PowerSourceKind.Ac));

        Assert.Equal(AutomationPreviewStatus.WouldSelect, preview.Status);
        Assert.Equal(PowerSourceKind.Ac, preview.PowerSource);
        Assert.Equal("High AC", preview.SelectedRule?.Name);
        Assert.Same(highProfile, preview.SelectedProfile);
        Assert.Null(preview.ReasonCode);
    }

    [Fact]
    public void Missing_power_observation_blocks_preview()
    {
        AutomationPreview preview = CreateService().Create(
            [],
            [],
            powerSourceSnapshot: null);

        Assert.Equal(AutomationPreviewStatus.PowerSourceUnavailable, preview.Status);
        Assert.Equal("power_source_not_read", preview.ReasonCode);
    }

    [Fact]
    public void Failed_power_observation_preserves_its_error()
    {
        var snapshot = new PowerSourceSnapshot(
            Now,
            HardwareReadResult<PowerSourceKind>.Failure(
                HardwareReadStatus.Unavailable,
                "power_source_unknown"));

        AutomationPreview preview = CreateService().Create([], [], snapshot);

        Assert.Equal(AutomationPreviewStatus.PowerSourceUnavailable, preview.Status);
        Assert.Equal("power_source_unknown", preview.ReasonCode);
    }

    [Fact]
    public void Stale_power_observation_blocks_preview()
    {
        PowerSourceSnapshot snapshot = CreatePowerSource(
            PowerSourceKind.Ac,
            Now - AutomationPreviewService.DefaultMaximumPowerSourceAge
                - TimeSpan.FromSeconds(1));

        AutomationPreview preview = CreateService().Create([], [], snapshot);

        Assert.Equal(AutomationPreviewStatus.PowerSourceStale, preview.Status);
        Assert.Equal("power_source_stale", preview.ReasonCode);
    }

    [Fact]
    public void Future_power_observation_is_invalid()
    {
        PowerSourceSnapshot snapshot = CreatePowerSource(
            PowerSourceKind.Ac,
            Now + TimeSpan.FromSeconds(1));

        AutomationPreview preview = CreateService().Create([], [], snapshot);

        Assert.Equal(AutomationPreviewStatus.PowerSourceStale, preview.Status);
        Assert.Equal("power_source_timestamp_invalid", preview.ReasonCode);
    }

    [Fact]
    public void No_enabled_rules_is_distinct_from_no_match()
    {
        HardwareProfile profile = CreateProfile("Only");
        AutomationRule disabled = CreateRule(
            "Disabled",
            profile.Id,
            PowerSourceKind.Ac,
            priority: 1,
            isEnabled: false);

        AutomationPreview preview = CreateService().Create(
            [disabled],
            [profile],
            CreatePowerSource(PowerSourceKind.Ac));

        Assert.Equal(AutomationPreviewStatus.NoEnabledRules, preview.Status);
        Assert.Equal("automation_no_enabled_rules", preview.ReasonCode);
    }

    [Fact]
    public void Enabled_rules_for_another_source_report_no_match()
    {
        HardwareProfile profile = CreateProfile("Battery");
        AutomationRule rule = CreateRule(
            "On battery",
            profile.Id,
            PowerSourceKind.Battery,
            priority: 1);

        AutomationPreview preview = CreateService().Create(
            [rule],
            [profile],
            CreatePowerSource(PowerSourceKind.Ac));

        Assert.Equal(AutomationPreviewStatus.NoMatch, preview.Status);
        Assert.Equal("automation_no_matching_rule", preview.ReasonCode);
    }

    [Fact]
    public void Equal_highest_priorities_are_ambiguous()
    {
        HardwareProfile first = CreateProfile("First");
        HardwareProfile second = CreateProfile("Second");
        AutomationRule[] rules =
        [
            CreateRule("First AC", first.Id, PowerSourceKind.Ac, priority: 10),
            CreateRule("Second AC", second.Id, PowerSourceKind.Ac, priority: 10),
        ];

        AutomationPreview preview = CreateService().Create(
            rules,
            [first, second],
            CreatePowerSource(PowerSourceKind.Ac));

        Assert.Equal(AutomationPreviewStatus.Ambiguous, preview.Status);
        Assert.Equal("automation_priority_ambiguous", preview.ReasonCode);
        Assert.Null(preview.SelectedRule);
    }

    [Fact]
    public void Duplicate_enabled_rule_ids_are_ambiguous()
    {
        HardwareProfile profile = CreateProfile("Profile");
        AutomationRuleId id = AutomationRuleId.New();
        AutomationRule[] rules =
        [
            new(id, "First", profile.Id, PowerSourceKind.Ac, 10),
            new(id, "Duplicate", profile.Id, PowerSourceKind.Battery, 20),
        ];

        AutomationPreview preview = CreateService().Create(
            rules,
            [profile],
            CreatePowerSource(PowerSourceKind.Ac));

        Assert.Equal(AutomationPreviewStatus.Ambiguous, preview.Status);
        Assert.Equal("automation_rule_id_duplicated", preview.ReasonCode);
    }

    [Fact]
    public void Missing_target_profile_blocks_selection()
    {
        ProfileId missingProfileId = ProfileId.New();
        AutomationRule rule = CreateRule(
            "Missing",
            missingProfileId,
            PowerSourceKind.Ac,
            priority: 1);

        AutomationPreview preview = CreateService().Create(
            [rule],
            [],
            CreatePowerSource(PowerSourceKind.Ac));

        Assert.Equal(AutomationPreviewStatus.ProfileMissing, preview.Status);
        Assert.Equal("automation_profile_missing", preview.ReasonCode);
    }

    [Fact]
    public void Rule_rejects_invalid_identifiers_names_sources_and_priorities()
    {
        ProfileId profileId = ProfileId.New();

        Assert.Throws<ArgumentException>(() => new AutomationRule(
            default,
            "Invalid",
            profileId,
            PowerSourceKind.Ac,
            1));
        Assert.Throws<ArgumentException>(() => new AutomationRule(
            AutomationRuleId.New(),
            " ",
            profileId,
            PowerSourceKind.Ac,
            1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomationRule(
            AutomationRuleId.New(),
            "Invalid source",
            profileId,
            (PowerSourceKind)99,
            1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomationRule(
            AutomationRuleId.New(),
            "Invalid priority",
            profileId,
            PowerSourceKind.Ac,
            AutomationRule.MaximumPriority + 1));
    }

    private static AutomationPreviewService CreateService() =>
        new(new FixedTimeProvider(Now));

    private static HardwareProfile CreateProfile(string name) =>
        new(
            ProfileId.New(),
            name,
            new HardwareProfileTargets(thermalMode: ThermalMode.Balanced));

    private static AutomationRule CreateRule(
        string name,
        ProfileId profileId,
        PowerSourceKind source,
        int priority,
        bool isEnabled = true) =>
        new(
            AutomationRuleId.New(),
            name,
            profileId,
            source,
            priority,
            isEnabled);

    private static PowerSourceSnapshot CreatePowerSource(
        PowerSourceKind source,
        DateTimeOffset? observedAt = null) =>
        new(
            observedAt ?? Now,
            HardwareReadResult<PowerSourceKind>.Success(source));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
