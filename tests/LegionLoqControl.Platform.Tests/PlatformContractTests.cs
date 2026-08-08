using LegionLoqControl.Application.Abstractions;
using LegionLoqControl.Application.Diagnostics;
using LegionLoqControl.Contracts.Broker;
using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using Xunit;

namespace LegionLoqControl.Platform.Tests;

public sealed class PlatformContractTests
{
    [Fact]
    public void Unknown_observations_are_not_encoded_as_values()
    {
        Observation observation = Observation.Unavailable("wmi_property_missing");

        Assert.Equal(ObservationState.Unavailable, observation.State);
        Assert.Null(observation.Value);
        Assert.Equal("wmi_property_missing", observation.ErrorCode);
    }

    [Fact]
    public void Hardware_value_objects_reject_unsafe_ranges_and_ids()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardBrightness(10));
        Assert.Throws<ArgumentException>(() => new CommandId(Guid.Empty));
    }

    [Fact]
    public void Broker_commands_require_an_expected_state()
    {
        CommandId commandId = CommandId.New();
        var command = new SetBatteryChargeModeCommand(
            commandId,
            BatteryChargeMode.Normal,
            BatteryChargeMode.Conservation);

        Assert.Equal(commandId, command.CommandId);
        Assert.Equal(BatteryChargeMode.Normal, command.Expected);
        Assert.Equal(BatteryChargeMode.Conservation, command.Desired);
    }

    [Fact]
    public async Task Diagnostics_convert_probe_failures_to_unknown_evidence()
    {
        DateTimeOffset now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
        var service = new MachineDiagnosticsService(
            new StubIdentitySource(),
            [new FailingProbe()],
            new FixedTimeProvider(now));

        MachineSnapshot snapshot = await service.CaptureAsync(TestContext.Current.CancellationToken);

        CapabilityEvidence evidence = Assert.Single(snapshot.Capabilities);
        Assert.Equal(HardwareCapability.ThermalMode, evidence.Capability);
        Assert.Equal(CapabilitySupport.Unknown, evidence.Support);
        Assert.Equal("probe_failed", evidence.EvidenceCode);
        Assert.Equal(now, evidence.ObservedAt);
    }

    private sealed class StubIdentitySource : IMachineIdentitySource
    {
        public string SourceName => "test";

        public ValueTask<MachineIdentity> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Observation value = Observation.FromValue("Test");
            return ValueTask.FromResult(new MachineIdentity(value, value, value, value, value));
        }
    }

    private sealed class FailingProbe : ICapabilityProbe
    {
        public string SourceName => "failing_test_probe";

        public IReadOnlySet<HardwareCapability> Capabilities { get; } =
            new HashSet<HardwareCapability> { HardwareCapability.ThermalMode };

        public ValueTask<IReadOnlyCollection<CapabilityEvidence>> ProbeAsync(
            MachineIdentity identity,
            CancellationToken cancellationToken) =>
            ValueTask.FromException<IReadOnlyCollection<CapabilityEvidence>>(
                new InvalidOperationException("Simulated failure"));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
