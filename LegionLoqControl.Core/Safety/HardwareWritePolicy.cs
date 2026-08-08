using global::System;

namespace LegionLoqControl.Core.Safety
{
    /// <summary>
    /// Release-wide hardware write policy for the migration prototype.
    /// </summary>
    public static class HardwareWritePolicy
    {
        public const string DisabledReason =
            "Hardware writes are disabled while the safety broker is being built.";

        public static bool IsEnabled => false;

        public static void Demand(string operation)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operation);
            throw new HardwareWriteDisabledException(operation, DisabledReason);
        }
    }

    public sealed class HardwareWriteDisabledException : InvalidOperationException
    {
        public HardwareWriteDisabledException(string operation, string reason)
            : base($"{operation} was blocked. {reason}")
        {
            Operation = operation;
        }

        public string Operation { get; }
    }
}
