using LegionLoqControl.Domain.Automation;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Automation;
using Xunit;

namespace LegionLoqControl.Platform.Tests;

public sealed class PowerSourceReaderTests
{
    [Theory]
    [InlineData(0, PowerSourceKind.Battery)]
    [InlineData(1, PowerSourceKind.Ac)]
    public void Documented_ac_line_values_map_to_typed_sources(
        byte rawValue,
        PowerSourceKind expected)
    {
        HardwareReadResult<PowerSourceKind> result =
            WindowsPowerSourceReader.MapAcLineStatus(rawValue);

        Assert.Equal(HardwareReadStatus.Success, result.Status);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Unknown_ac_line_value_fails_closed()
    {
        HardwareReadResult<PowerSourceKind> result =
            WindowsPowerSourceReader.MapAcLineStatus(byte.MaxValue);

        Assert.Equal(HardwareReadStatus.Unavailable, result.Status);
        Assert.Equal("power_source_unknown", result.ErrorCode);
    }

    [Fact]
    public void Undocumented_ac_line_value_is_invalid_data()
    {
        HardwareReadResult<PowerSourceKind> result =
            WindowsPowerSourceReader.MapAcLineStatus(2);

        Assert.Equal(HardwareReadStatus.InvalidData, result.Status);
        Assert.Equal("power_source_value_invalid", result.ErrorCode);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(54, 54)]
    [InlineData(100, 100)]
    public void Documented_battery_percents_map_to_the_same_value(byte rawValue, int expected)
    {
        Assert.Equal(expected, WindowsPowerSourceReader.MapBatteryPercent(rawValue));
    }

    [Theory]
    [InlineData(101)]
    [InlineData(254)]
    [InlineData(255)]
    public void Unknown_or_invalid_battery_percents_fail_closed(byte rawValue)
    {
        Assert.Null(WindowsPowerSourceReader.MapBatteryPercent(rawValue));
    }

    [Fact]
    public async Task Windows_api_read_returns_a_typed_outcome()
    {
        var reader = new WindowsPowerSourceReader();

        HardwareReadResult<PowerSourceKind> result = await reader.ReadAsync(
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Status,
            new[]
            {
                HardwareReadStatus.Success,
                HardwareReadStatus.Unavailable,
                HardwareReadStatus.Failed,
            });
        if (result.Status == HardwareReadStatus.Success)
            Assert.True(Enum.IsDefined(result.Value!.Value));
        else
            Assert.False(string.IsNullOrWhiteSpace(result.ErrorCode));
    }

    [Fact]
    public async Task Windows_api_telemetry_returns_a_bounded_percent()
    {
        var reader = new WindowsPowerSourceReader();

        SystemPowerTelemetry telemetry = await reader.ReadTelemetryAsync(
            TestContext.Current.CancellationToken);

        if (telemetry.BatteryPercent is { } percent)
            Assert.InRange(percent, 0, 100);
        if (telemetry.Source.Status == HardwareReadStatus.Success)
            Assert.True(Enum.IsDefined(telemetry.Source.Value!.Value));
    }
}

