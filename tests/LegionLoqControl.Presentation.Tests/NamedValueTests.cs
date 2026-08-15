using LegionLoqControl.ViewModels;
using Xunit;

namespace LegionLoqControl.Presentation.Tests;

public sealed class NamedValueTests
{
    [Fact]
    public void ToString_returns_the_label()
    {
        Assert.Equal("Static", new NamedValue("Static", "Static").ToString());
        Assert.Equal("High", new NamedValue("High", "High").ToString());
    }
}
