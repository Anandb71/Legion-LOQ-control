using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Application.Profiles;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Profiles;
using Xunit;

namespace LegionLoqControl.Application.Tests.Profiles;

public sealed class ProfileApplyPlannerTests
{
    [Fact]
    public void Matching_targets_produce_an_empty_plan()
    {
        ProfilePreview preview = CreatePreview(
            ProfileTargetPreview<BatteryChargeMode>.Matches(BatteryChargeMode.Normal),
            ProfileTargetPreview<ThermalMode>.Matches(ThermalMode.Balanced));

        Assert.Empty(ProfileApplyPlanner.Plan(preview));
    }

    [Fact]
    public void Would_change_battery_then_thermal()
    {
        ProfilePreview preview = CreatePreview(
            ProfileTargetPreview<BatteryChargeMode>.WouldChange(
                BatteryChargeMode.Normal,
                BatteryChargeMode.Conservation),
            ProfileTargetPreview<ThermalMode>.WouldChange(
                ThermalMode.Balanced,
                ThermalMode.Quiet));

        IReadOnlyList<HardwareWritePlanItem> operations = ProfileApplyPlanner.Plan(preview);

        Assert.Equal(2, operations.Count);
        Assert.Equal(HardwareWriteKind.BatteryChargeMode, operations[0].Kind);
        Assert.Equal(nameof(BatteryChargeMode.Normal), operations[0].Expected);
        Assert.Equal(nameof(BatteryChargeMode.Conservation), operations[0].Desired);
        Assert.Equal(HardwareWriteKind.ThermalMode, operations[1].Kind);
        Assert.Equal(nameof(ThermalMode.Balanced), operations[1].Expected);
        Assert.Equal(nameof(ThermalMode.Quiet), operations[1].Desired);
    }

    [Fact]
    public void Blocked_preview_refuses_to_plan()
    {
        ProfilePreview preview = CreatePreview(
            battery: null,
            ProfileTargetPreview<ThermalMode>.Blocked(
                ProfileTargetPreviewState.Unverified,
                ThermalMode.Quiet,
                ThermalMode.Balanced,
                "capability_evidence_missing"));

        HardwareWriteException exception = Assert.Throws<HardwareWriteException>(
            () => ProfileApplyPlanner.Plan(preview));

        Assert.Equal("profile_apply_blocked", exception.ErrorCode);
        Assert.Equal(HardwareWriteStatus.Conflict, exception.Status);
    }

    [Fact]
    public void Custom_thermal_change_is_unsupported()
    {
        ProfilePreview preview = CreatePreview(
            battery: null,
            ProfileTargetPreview<ThermalMode>.WouldChange(
                ThermalMode.Balanced,
                ThermalMode.Custom));

        HardwareWriteException exception = Assert.Throws<HardwareWriteException>(
            () => ProfileApplyPlanner.Plan(preview));

        Assert.Equal("thermal_custom_unsupported", exception.ErrorCode);
        Assert.Equal(HardwareWriteStatus.Unsupported, exception.Status);
    }

    private static ProfilePreview CreatePreview(
        ProfileTargetPreview<BatteryChargeMode>? battery,
        ProfileTargetPreview<ThermalMode>? thermal) =>
        new(
            new HardwareProfile(
                ProfileId.New(),
                "Test",
                new HardwareProfileTargets(
                    battery?.Desired,
                    thermal?.Desired)),
            new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero),
            battery,
            thermal);
}
