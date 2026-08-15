using LegionLoqControl.Application.Automation;
using LegionLoqControl.Domain.Automation;
using LegionLoqControl.Domain.Profiles;
using Xunit;

namespace LegionLoqControl.Application.Tests.Automation;

public sealed class AutomationRunServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void First_would_change_on_a_power_source_is_applied()
    {
        var time = new ControllableTimeProvider(Now);
        var service = new AutomationRunService(time, TimeSpan.FromMinutes(2));

        Assert.Equal(
            AutomationRunVerdict.Apply,
            service.Evaluate(ProfileId.New(), PowerSourceKind.Ac, hasWouldChangeOperations: true));
    }

    [Fact]
    public void Matching_hardware_is_remembered_without_a_write()
    {
        var service = new AutomationRunService(new ControllableTimeProvider(Now));
        ProfileId profileId = ProfileId.New();

        Assert.Equal(
            AutomationRunVerdict.SkipUnchanged,
            service.Evaluate(profileId, PowerSourceKind.Battery, hasWouldChangeOperations: false));
        Assert.Equal(profileId, service.LastAppliedProfileId);
        Assert.Equal(PowerSourceKind.Battery, service.LastAppliedPowerSource);
        Assert.Equal(
            AutomationRunVerdict.SkipUnchanged,
            service.Evaluate(profileId, PowerSourceKind.Battery, hasWouldChangeOperations: true));
    }

    [Fact]
    public void Successful_apply_enters_cooldown_then_allows_the_other_power_source()
    {
        var time = new ControllableTimeProvider(Now);
        var service = new AutomationRunService(time, TimeSpan.FromMinutes(2));
        ProfileId acProfile = ProfileId.New();
        ProfileId batteryProfile = ProfileId.New();

        service.NoteSuccess(acProfile, PowerSourceKind.Ac);

        Assert.Equal(
            AutomationRunVerdict.SkipCooldown,
            service.Evaluate(batteryProfile, PowerSourceKind.Battery, hasWouldChangeOperations: true));

        time.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(
            AutomationRunVerdict.Apply,
            service.Evaluate(batteryProfile, PowerSourceKind.Battery, hasWouldChangeOperations: true));
    }

    [Fact]
    public void Readback_failure_suspends_until_resume()
    {
        var service = new AutomationRunService(new ControllableTimeProvider(Now));
        service.NoteFailure("battery_readback_mismatch");

        Assert.True(service.IsSuspended);
        Assert.Equal("battery_readback_mismatch", service.SuspendReason);
        Assert.Equal(
            AutomationRunVerdict.Suspended,
            service.Evaluate(ProfileId.New(), PowerSourceKind.Ac, hasWouldChangeOperations: true));

        service.Resume();

        Assert.False(service.IsSuspended);
        Assert.Equal(
            AutomationRunVerdict.SkipCooldown,
            service.Evaluate(ProfileId.New(), PowerSourceKind.Ac, hasWouldChangeOperations: true));
    }

    [Fact]
    public void Elevation_cancel_cools_down_without_suspending()
    {
        var service = new AutomationRunService(new ControllableTimeProvider(Now));
        service.NoteCancel();

        Assert.False(service.IsSuspended);
        Assert.Equal(
            AutomationRunVerdict.SkipCooldown,
            service.Evaluate(ProfileId.New(), PowerSourceKind.Ac, hasWouldChangeOperations: true));
    }

    [Fact]
    public void Expected_mismatch_does_not_suspend()
    {
        var service = new AutomationRunService(new ControllableTimeProvider(Now));
        service.NoteFailure("thermal_expected_mismatch");

        Assert.False(service.IsSuspended);
        Assert.Equal(
            AutomationRunVerdict.SkipCooldown,
            service.Evaluate(ProfileId.New(), PowerSourceKind.Ac, hasWouldChangeOperations: true));
    }

    private sealed class ControllableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }
}
