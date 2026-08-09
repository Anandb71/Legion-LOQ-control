using LegionLoqControl.Application.Automation;
using LegionLoqControl.Domain.Automation;
using LegionLoqControl.Domain.Results;
using Xunit;

namespace LegionLoqControl.Application.Tests.Automation;

public sealed class PowerSourceServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Capture_preserves_typed_state_and_observation_time()
    {
        var reader = new StubPowerSourceReader(
            HardwareReadResult<PowerSourceKind>.Success(PowerSourceKind.Ac));
        var service = new PowerSourceService(
            reader,
            new FixedTimeProvider(Now));

        PowerSourceSnapshot snapshot = await service.CaptureAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(Now, snapshot.ObservedAt);
        Assert.Equal(HardwareReadStatus.Success, snapshot.PowerSource.Status);
        Assert.Equal(PowerSourceKind.Ac, snapshot.PowerSource.Value);
        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task Capture_preserves_fail_closed_read_results()
    {
        var reader = new StubPowerSourceReader(
            HardwareReadResult<PowerSourceKind>.Failure(
                HardwareReadStatus.Unavailable,
                "power_source_unknown"));
        var service = new PowerSourceService(
            reader,
            new FixedTimeProvider(Now));

        PowerSourceSnapshot snapshot = await service.CaptureAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HardwareReadStatus.Unavailable, snapshot.PowerSource.Status);
        Assert.False(snapshot.PowerSource.HasValue);
        Assert.Equal("power_source_unknown", snapshot.PowerSource.ErrorCode);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubPowerSourceReader(
        HardwareReadResult<PowerSourceKind> result) : IPowerSourceReader
    {
        public int ReadCount { get; private set; }

        public ValueTask<HardwareReadResult<PowerSourceKind>> ReadAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            return ValueTask.FromResult(result);
        }
    }
}

