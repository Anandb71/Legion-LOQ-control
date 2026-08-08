using LegionLoqControl.Core.Hardware;
using LegionLoqControl.Core.Safety;
using Xunit;

namespace LegionLoqControl.Core.Tests.Safety;

public sealed class HardwareWritePolicyTests
{
    [Fact]
    public void Prototype_policy_is_immutable_and_disabled()
    {
        Assert.False(HardwareWritePolicy.IsEnabled);
        Assert.NotEmpty(HardwareWritePolicy.DisabledReason);
    }

    [Fact]
    public void Demand_reports_the_blocked_operation()
    {
        HardwareWriteDisabledException exception = Assert.Throws<HardwareWriteDisabledException>(
            () => HardwareWritePolicy.Demand("Set test feature"));

        Assert.Equal("Set test feature", exception.Operation);
        Assert.Contains(HardwareWritePolicy.DisabledReason, exception.Message);
    }

    [Fact]
    public void Battery_commands_are_blocked_before_driver_access()
    {
        var controller = new BatteryController();

        Assert.Throws<HardwareWriteDisabledException>(() => controller.SetConservationMode(true));
        Assert.Throws<HardwareWriteDisabledException>(() => controller.SetRapidCharge(true));
    }

    [Fact]
    public void Hid_commands_are_blocked_before_device_discovery()
    {
        var fourZone = new LightingController();
        var spectrum = new SpectrumKeyboardController();

        Assert.Throws<HardwareWriteDisabledException>(() => fourZone.SetValues(1, 255, 255, 255));
        Assert.Throws<HardwareWriteDisabledException>(() => fourZone.SetOff());
        Assert.Throws<HardwareWriteDisabledException>(() => spectrum.SetBrightness(5));
    }

    [Fact]
    public async Task Wmi_commands_fail_closed_before_provider_access()
    {
        var power = new PowerController();
        var fans = new CustomModeController();
        var lighting = new LightingController();

        Assert.False(await power.SetProfileAsync(PowerProfile.Balanced));
        Assert.False(await fans.SetFanFullSpeedAsync(true));
        Assert.False(await lighting.SetLightingOwnerAsync(true));
    }
}
