using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Profiles;
using Xunit;

namespace LegionLoqControl.Application.Tests.Profiles;

public sealed class HardwareProfileTests
{
    [Fact]
    public void Valid_profile_preserves_bounded_targets_and_normalizes_its_name()
    {
        var targets = new HardwareProfileTargets(
            BatteryChargeMode.Conservation,
            ThermalMode.Quiet);
        var profile = new HardwareProfile(
            ProfileId.New(),
            "  Battery saver  ",
            targets);

        Assert.NotEqual(Guid.Empty, profile.Id.Value);
        Assert.Equal("Battery saver", profile.Name);
        Assert.Equal("Battery saver", profile.ToString());
        Assert.Equal(BatteryChargeMode.Conservation, profile.Targets.BatteryChargeMode);
        Assert.Equal(ThermalMode.Quiet, profile.Targets.ThermalMode);
    }

    [Fact]
    public void Profile_requires_a_non_empty_id()
    {
        Assert.Throws<ArgumentException>(() => new ProfileId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new HardwareProfile(
            default,
            "Invalid",
            new HardwareProfileTargets(thermalMode: ThermalMode.Balanced)));
    }

    [Fact]
    public void Profile_requires_at_least_one_target()
    {
        Assert.Throws<ArgumentException>(() => new HardwareProfileTargets());
    }

    [Fact]
    public void Profile_rejects_unknown_hardware_modes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HardwareProfileTargets(batteryChargeMode: (BatteryChargeMode)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HardwareProfileTargets(thermalMode: (ThermalMode)99));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\u0000name")]
    public void Profile_rejects_invalid_names(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new HardwareProfile(
            ProfileId.New(),
            name,
            new HardwareProfileTargets(thermalMode: ThermalMode.Balanced)));
    }

    [Fact]
    public void Profile_rejects_names_over_the_limit()
    {
        string name = new('A', HardwareProfile.MaximumNameLength + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new HardwareProfile(
            ProfileId.New(),
            name,
            new HardwareProfileTargets(thermalMode: ThermalMode.Balanced)));
    }
}

