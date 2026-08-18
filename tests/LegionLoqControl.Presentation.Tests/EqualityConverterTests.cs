using System.Globalization;
using LegionLoqControl.Converters;
using Xunit;

namespace LegionLoqControl.Presentation.Tests;

public sealed class EqualityConverterTests
{
    private readonly EqualityConverter _converter = new();

    [Fact]
    public void Current_chip_matches_the_option_label()
    {
        Assert.Equal(
            true,
            _converter.Convert(
                ["Conservation", "Conservation", "Conservation"],
                typeof(bool),
                parameter: null!,
                CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Toggle_chip_matches_disabled_token_when_the_card_shows_disabled()
    {
        Assert.Equal(
            true,
            _converter.Convert(
                ["Off", "Disabled", "Disabled"],
                typeof(bool),
                parameter: null!,
                CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Unrelated_option_stays_inactive()
    {
        Assert.Equal(
            false,
            _converter.Convert(
                ["On", "Enabled", "Disabled"],
                typeof(bool),
                parameter: null!,
                CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_is_not_supported()
    {
        Assert.Throws<NotSupportedException>(
            () => _converter.ConvertBack(
                true,
                [typeof(string), typeof(string)],
                parameter: null!,
                CultureInfo.InvariantCulture));
    }
}
