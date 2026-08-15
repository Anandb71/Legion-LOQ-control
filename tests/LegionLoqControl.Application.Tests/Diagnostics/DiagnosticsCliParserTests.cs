using LegionLoqControl.Application.Diagnostics;
using Xunit;

namespace LegionLoqControl.Application.Tests.Diagnostics;

public sealed class DiagnosticsCliParserTests
{
    [Fact]
    public void Empty_args_select_inventory_on_stdout()
    {
        DiagnosticsCliParseResult result = DiagnosticsCliParser.Parse([]);

        Assert.True(result.IsValid);
        Assert.Equal(DiagnosticsCliVerb.Inventory, result.Verb);
        Assert.Null(result.OutputPath);
    }

    [Fact]
    public void Inventory_accepts_an_absolute_output_path()
    {
        string destination = Path.Combine(Path.GetTempPath(), "inventory.json");

        DiagnosticsCliParseResult result = DiagnosticsCliParser.Parse(
            ["inventory", "--output", destination]);

        Assert.True(result.IsValid);
        Assert.Equal(DiagnosticsCliVerb.Inventory, result.Verb);
        Assert.Equal(destination, result.OutputPath);
    }

    [Theory]
    [InlineData("inventory", "--output", "inventory.json")]
    [InlineData("inventory", "--output")]
    [InlineData("state", "--output", @"C:\temp\state.json")]
    [InlineData("inventory", "extra")]
    [InlineData("unknown")]
    public void Invalid_shapes_are_rejected(params string[] args)
    {
        DiagnosticsCliParseResult result = DiagnosticsCliParser.Parse(args);

        Assert.False(result.IsValid);
        Assert.Null(result.OutputPath);
    }

    [Fact]
    public void Help_is_accepted_alone()
    {
        DiagnosticsCliParseResult result = DiagnosticsCliParser.Parse(["--help"]);

        Assert.True(result.IsValid);
        Assert.Equal(DiagnosticsCliVerb.Help, result.Verb);
    }
}
