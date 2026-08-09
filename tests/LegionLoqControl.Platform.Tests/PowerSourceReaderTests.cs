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
}

