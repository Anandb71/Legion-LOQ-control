using LegionLoqControl.Application.Profiles;
using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Profiles;
using LegionLoqControl.Domain.Results;
using Xunit;

namespace LegionLoqControl.Application.Tests.Profiles;

public sealed class ProfilePreviewServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Matching_targets_do_not_require_write_capability_evidence()
    {
        ProfilePreview preview = CreateService().Create(
            CreateProfile(BatteryChargeMode.Normal, ThermalMode.Balanced),
            CreateSnapshot(Now),
            []);

        Assert.Equal(ProfileTargetPreviewState.Matches, preview.BatteryChargeMode?.State);
        Assert.Equal(ProfileTargetPreviewState.Matches, preview.ThermalMode?.State);
    }

    [Fact]
    public void Verified_differences_are_reported_without_applying_them()
    {
        ProfilePreview preview = CreateService().Create(
            CreateProfile(BatteryChargeMode.Conservation, ThermalMode.Quiet),
            CreateSnapshot(Now),
            [
                Evidence(
                    HardwareCapability.BatteryConservationMode,
                    CapabilitySupport.Supported,
                    "battery_verified"),
                Evidence(
                    HardwareCapability.ThermalMode,
                    CapabilitySupport.Supported,
                    "thermal_verified"),
            ]);

        Assert.Equal(ProfileTargetPreviewState.WouldChange, preview.BatteryChargeMode?.State);
        Assert.Equal(BatteryChargeMode.Normal, preview.BatteryChargeMode?.Current);
        Assert.Equal(BatteryChargeMode.Conservation, preview.BatteryChargeMode?.Desired);
        Assert.Equal(ProfileTargetPreviewState.WouldChange, preview.ThermalMode?.State);
        Assert.Equal(ThermalMode.Balanced, preview.ThermalMode?.Current);
        Assert.Equal(ThermalMode.Quiet, preview.ThermalMode?.Desired);
    }

    [Fact]
    public void Unknown_capability_blocks_a_different_target()
    {
        ProfilePreview preview = CreateService().Create(
            CreateProfile(thermal: ThermalMode.Quiet),
            CreateSnapshot(Now),
            [
                Evidence(
                    HardwareCapability.ThermalMode,
                    CapabilitySupport.Unknown,
                    "wmi_interface_present_unverified"),
            ]);

        Assert.Equal(ProfileTargetPreviewState.Unverified, preview.ThermalMode?.State);
        Assert.Equal("wmi_interface_present_unverified", preview.ThermalMode?.ReasonCode);
    }

    [Fact]
    public void Unsupported_capability_marks_a_target_unavailable()
    {
        ProfilePreview preview = CreateService().Create(
            CreateProfile(thermal: ThermalMode.Quiet),
            CreateSnapshot(Now),
            [
                Evidence(
                    HardwareCapability.ThermalMode,
                    CapabilitySupport.Unsupported,
                    "wmi_interface_not_found"),
            ]);

        Assert.Equal(ProfileTargetPreviewState.Unavailable, preview.ThermalMode?.State);
        Assert.Equal("wmi_interface_not_found", preview.ThermalMode?.ReasonCode);
    }

    [Fact]
    public void Missing_snapshot_is_explicitly_unavailable()
    {
        ProfilePreview preview = CreateService().Create(
            CreateProfile(BatteryChargeMode.Conservation, ThermalMode.Quiet),
            snapshot: null,
            []);

        Assert.Equal(ProfileTargetPreviewState.Unavailable, preview.BatteryChargeMode?.State);
        Assert.Equal("hardware_state_not_read", preview.BatteryChargeMode?.ReasonCode);
        Assert.Equal(ProfileTargetPreviewState.Unavailable, preview.ThermalMode?.State);
        Assert.Equal("hardware_state_not_read", preview.ThermalMode?.ReasonCode);
    }

    [Fact]
    public void Read_failure_preserves_its_stable_error_code()
    {
        HardwareStateSnapshot snapshot = CreateSnapshot(
            Now,
            thermal: HardwareReadResult<ThermalMode>.Failure(
                HardwareReadStatus.AccessDenied,
                "wmi_access_denied"));

        ProfilePreview preview = CreateService().Create(
            CreateProfile(thermal: ThermalMode.Quiet),
            snapshot,
            []);

        Assert.Equal(ProfileTargetPreviewState.Unavailable, preview.ThermalMode?.State);
        Assert.Equal("wmi_access_denied", preview.ThermalMode?.ReasonCode);
        Assert.Null(preview.ThermalMode?.Current);
    }

    [Fact]
    public void Stale_snapshot_cannot_claim_a_match()
    {
        ProfilePreview preview = CreateService().Create(
            CreateProfile(thermal: ThermalMode.Balanced),
            CreateSnapshot(Now - ProfilePreviewService.DefaultMaximumSnapshotAge - TimeSpan.FromSeconds(1)),
            []);

        Assert.Equal(ProfileTargetPreviewState.Stale, preview.ThermalMode?.State);
        Assert.Equal("hardware_state_stale", preview.ThermalMode?.ReasonCode);
        Assert.Equal(ThermalMode.Balanced, preview.ThermalMode?.Current);
    }

    [Fact]
    public void Future_snapshot_timestamp_is_rejected()
    {
        ProfilePreview preview = CreateService().Create(
            CreateProfile(thermal: ThermalMode.Balanced),
            CreateSnapshot(Now + TimeSpan.FromSeconds(1)),
            []);

        Assert.Equal(ProfileTargetPreviewState.Stale, preview.ThermalMode?.State);
        Assert.Equal("hardware_state_timestamp_invalid", preview.ThermalMode?.ReasonCode);
    }

    [Fact]
    public void Ambiguous_capability_evidence_fails_closed()
    {
        ProfilePreview preview = CreateService().Create(
            CreateProfile(thermal: ThermalMode.Quiet),
            CreateSnapshot(Now),
            [
                Evidence(HardwareCapability.ThermalMode, CapabilitySupport.Supported, "source_a"),
                Evidence(HardwareCapability.ThermalMode, CapabilitySupport.Supported, "source_b"),
            ]);

        Assert.Equal(ProfileTargetPreviewState.Unverified, preview.ThermalMode?.State);
        Assert.Equal("capability_evidence_ambiguous", preview.ThermalMode?.ReasonCode);
    }

    [Fact]
    public void Switching_between_special_battery_modes_requires_both_capabilities()
    {
        HardwareStateSnapshot snapshot = CreateSnapshot(
            Now,
            battery: HardwareReadResult<BatteryChargeMode>.Success(
                BatteryChargeMode.RapidCharge));
        ProfilePreview preview = CreateService().Create(
            CreateProfile(battery: BatteryChargeMode.Conservation),
            snapshot,
            [
                Evidence(
                    HardwareCapability.BatteryConservationMode,
                    CapabilitySupport.Supported,
                    "conservation_verified"),
            ]);

        Assert.Equal(ProfileTargetPreviewState.Unverified, preview.BatteryChargeMode?.State);
        Assert.Equal("capability_evidence_missing", preview.BatteryChargeMode?.ReasonCode);
    }

    [Fact]
    public void Preview_only_contains_targets_selected_by_the_profile()
    {
        ProfilePreview preview = CreateService().Create(
            CreateProfile(thermal: ThermalMode.Quiet),
            CreateSnapshot(Now),
            [
                Evidence(
                    HardwareCapability.ThermalMode,
                    CapabilitySupport.Supported,
                    "thermal_verified"),
            ]);

        Assert.Null(preview.BatteryChargeMode);
        Assert.NotNull(preview.ThermalMode);
    }

    private static ProfilePreviewService CreateService() =>
        new(new FixedTimeProvider(Now));

    private static HardwareProfile CreateProfile(
        BatteryChargeMode? battery = null,
        ThermalMode? thermal = null) =>
        new(
            ProfileId.New(),
            "Test profile",
            new HardwareProfileTargets(battery, thermal));

    private static HardwareStateSnapshot CreateSnapshot(
        DateTimeOffset observedAt,
        HardwareReadResult<BatteryChargeMode>? battery = null,
        HardwareReadResult<ThermalMode>? thermal = null) =>
        new(
            observedAt,
            battery ?? HardwareReadResult<BatteryChargeMode>.Success(BatteryChargeMode.Normal),
            thermal ?? HardwareReadResult<ThermalMode>.Success(ThermalMode.Balanced),
            HardwareReadResult<ToggleState>.Success(ToggleState.Disabled),
            HardwareReadResult<IntegratedGpuMode>.Success(IntegratedGpuMode.Default));

    private static CapabilityEvidence Evidence(
        HardwareCapability capability,
        CapabilitySupport support,
        string evidenceCode) =>
        new(
            capability,
            support,
            "test",
            Now,
            evidenceCode);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

