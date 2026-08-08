using System.Globalization;
using LegionLoqControl.Contracts.Broker;

namespace LegionLoqControl.Broker;

internal sealed record BrokerArguments(
    string PipeName,
    string Nonce,
    int ParentProcessId)
{
    public static bool TryParse(string[] args, out BrokerArguments? result)
    {
        ArgumentNullException.ThrowIfNull(args);
        result = null;
        if (args.Length != 6 ||
            !string.Equals(args[0], "--pipe", StringComparison.Ordinal) ||
            !string.Equals(args[2], "--nonce", StringComparison.Ordinal) ||
            !string.Equals(args[4], "--parent-pid", StringComparison.Ordinal))
        {
            return false;
        }

        if (!BrokerProtocol.IsValidPipeName(args[1]) ||
            !BrokerProtocol.IsValidNonce(args[3]) ||
            !int.TryParse(
                args[5],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int parentProcessId) ||
            parentProcessId <= 0)
        {
            return false;
        }

        result = new BrokerArguments(args[1], args[3], parentProcessId);
        return true;
    }
}
