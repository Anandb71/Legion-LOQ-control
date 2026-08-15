namespace LegionLoqControl.Application.Diagnostics;

public enum DiagnosticsCliVerb
{
    Inventory = 0,
    State = 1,
    StateElevated = 2,
    Help = 3,
}

public sealed record DiagnosticsCliParseResult(
    bool IsValid,
    DiagnosticsCliVerb Verb,
    string? OutputPath);

public static class DiagnosticsCliParser
{
    public const string Usage =
        "Usage: LegionLoqControl.Diagnostics [inventory|state|state-elevated] " +
        "[--output <absolute-path>]";

    public static DiagnosticsCliParseResult Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0)
            return new DiagnosticsCliParseResult(true, DiagnosticsCliVerb.Inventory, null);

        string verbToken = args[0].Trim();
        if (IsHelp(verbToken))
        {
            return args.Count == 1
                ? new DiagnosticsCliParseResult(true, DiagnosticsCliVerb.Help, null)
                : Invalid();
        }

        if (!TryResolveVerb(verbToken, out DiagnosticsCliVerb verb))
            return Invalid();

        if (args.Count == 1)
            return new DiagnosticsCliParseResult(true, verb, null);

        if (args.Count == 3 &&
            verb == DiagnosticsCliVerb.Inventory &&
            IsOutputFlag(args[1]))
        {
            string outputPath = args[2].Trim();
            if (string.IsNullOrWhiteSpace(outputPath) ||
                !Path.IsPathFullyQualified(outputPath))
            {
                return Invalid();
            }

            return new DiagnosticsCliParseResult(true, verb, outputPath);
        }

        return Invalid();
    }

    private static DiagnosticsCliParseResult Invalid() =>
        new(false, DiagnosticsCliVerb.Inventory, null);

    private static bool IsHelp(string value) =>
        value.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("-h", StringComparison.OrdinalIgnoreCase);

    private static bool IsOutputFlag(string value) =>
        value.Equals("--output", StringComparison.OrdinalIgnoreCase);

    private static bool TryResolveVerb(string value, out DiagnosticsCliVerb verb)
    {
        switch (value.ToLowerInvariant())
        {
            case "inventory":
                verb = DiagnosticsCliVerb.Inventory;
                return true;
            case "state":
                verb = DiagnosticsCliVerb.State;
                return true;
            case "state-elevated":
                verb = DiagnosticsCliVerb.StateElevated;
                return true;
            default:
                verb = DiagnosticsCliVerb.Inventory;
                return false;
        }
    }
}
