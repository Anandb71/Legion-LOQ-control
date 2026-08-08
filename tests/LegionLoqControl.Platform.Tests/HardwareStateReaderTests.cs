using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Hardware;
using Xunit;

namespace LegionLoqControl.Platform.Tests;

public sealed class HardwareStateReaderTests
{
    [Theory]
    [InlineData(1u, ThermalMode.Quiet)]
    [InlineData(2u, ThermalMode.Balanced)]
    [InlineData(3u, ThermalMode.Performance)]
    [InlineData(224u, ThermalMode.Extreme)]
    [InlineData(255u, ThermalMode.Custom)]
    public async Task Thermal_values_map_to_typed_modes(uint rawValue, ThermalMode expected)
    {
        var invoker = new StubInvoker();
        invoker.Values[LenovoWmiReadOperation.ThermalMode] = rawValue;
        var reader = new WindowsHardwareStateReader(invoker);

        HardwareReadResult<ThermalMode> result = await reader.ReadThermalModeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HardwareReadStatus.Success, result.Status);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task Battery_mode_is_not_inferred_from_an_unrelated_WMI_getter()
    {
        var invoker = new StubInvoker();
        var reader = new WindowsHardwareStateReader(invoker);

        HardwareReadResult<BatteryChargeMode> result = await reader.ReadBatteryChargeModeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HardwareReadStatus.Unavailable, result.Status);
        Assert.False(result.HasValue);
        Assert.Equal("battery_read_transport_not_implemented", result.ErrorCode);
        Assert.Empty(invoker.Operations);
    }

    [Fact]
    public void Getter_output_contract_requires_true_status_and_UInt32_data()
    {
        SystemLenovoWmiReadInvoker.ValidateReturnValue(true);
        Assert.Equal(42u, SystemLenovoWmiReadInvoker.ConvertDataToUInt32(42u));

        Assert.Throws<LenovoWmiMethodRejectedException>(
            () => SystemLenovoWmiReadInvoker.ValidateReturnValue(false));
        Assert.Throws<InvalidDataException>(
            () => SystemLenovoWmiReadInvoker.ValidateReturnValue(0u));
        Assert.Throws<InvalidDataException>(
            () => SystemLenovoWmiReadInvoker.ConvertDataToUInt32("42"));
    }

    [Fact]
    public async Task Unexpected_firmware_values_fail_as_invalid_data()
    {
        var invoker = new StubInvoker();
        invoker.Values[LenovoWmiReadOperation.DisplayOverdrive] = 7;
        var reader = new WindowsHardwareStateReader(invoker);

        HardwareReadResult<ToggleState> result = await reader.ReadDisplayOverdriveAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HardwareReadStatus.InvalidData, result.Status);
        Assert.False(result.HasValue);
        Assert.Equal("unexpected_overdrive_value", result.ErrorCode);
    }

    [Fact]
    public async Task Access_denied_is_distinct_from_unsupported_and_off()
    {
        var invoker = new StubInvoker
        {
            Exception = new UnauthorizedAccessException(),
        };
        var reader = new WindowsHardwareStateReader(invoker);

        HardwareReadResult<IntegratedGpuMode> result = await reader.ReadIntegratedGpuModeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HardwareReadStatus.AccessDenied, result.Status);
        Assert.False(result.HasValue);
        Assert.Equal("wmi_access_denied", result.ErrorCode);
    }

    [Fact]
    public async Task State_service_serializes_reads_in_a_stable_order()
    {
        DateTimeOffset now = new(2026, 8, 8, 12, 30, 0, TimeSpan.Zero);
        var reader = new OrderedStateReader();
        var service = new HardwareStateService(reader, new FixedTimeProvider(now));

        HardwareStateSnapshot snapshot = await service.CaptureAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ["battery", "thermal", "overdrive", "igpu"],
            reader.Operations);
        Assert.Equal(now, snapshot.ObservedAt);
        Assert.Equal(BatteryChargeMode.Normal, snapshot.BatteryChargeMode.Value);
        Assert.Equal(ThermalMode.Balanced, snapshot.ThermalMode.Value);
    }

    private sealed class StubInvoker : ILenovoWmiReadInvoker
    {
        public Dictionary<LenovoWmiReadOperation, uint> Values { get; } = [];

        public List<LenovoWmiReadOperation> Operations { get; } = [];

        public Exception? Exception { get; init; }

        public ValueTask<uint> ReadAsync(
            LenovoWmiReadOperation operation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Operations.Add(operation);
            if (Exception is not null)
                return ValueTask.FromException<uint>(Exception);

            return ValueTask.FromResult(Values[operation]);
        }
    }

    private sealed class OrderedStateReader : IHardwareStateReader
    {
        public List<string> Operations { get; } = [];

        public ValueTask<HardwareReadResult<BatteryChargeMode>> ReadBatteryChargeModeAsync(
            CancellationToken cancellationToken)
        {
            Operations.Add("battery");
            return ValueTask.FromResult(HardwareReadResult<BatteryChargeMode>.Success(BatteryChargeMode.Normal));
        }

        public ValueTask<HardwareReadResult<ThermalMode>> ReadThermalModeAsync(
            CancellationToken cancellationToken)
        {
            Operations.Add("thermal");
            return ValueTask.FromResult(HardwareReadResult<ThermalMode>.Success(ThermalMode.Balanced));
        }

        public ValueTask<HardwareReadResult<ToggleState>> ReadDisplayOverdriveAsync(
            CancellationToken cancellationToken)
        {
            Operations.Add("overdrive");
            return ValueTask.FromResult(HardwareReadResult<ToggleState>.Success(ToggleState.Disabled));
        }

        public ValueTask<HardwareReadResult<IntegratedGpuMode>> ReadIntegratedGpuModeAsync(
            CancellationToken cancellationToken)
        {
            Operations.Add("igpu");
            return ValueTask.FromResult(HardwareReadResult<IntegratedGpuMode>.Success(IntegratedGpuMode.Default));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
