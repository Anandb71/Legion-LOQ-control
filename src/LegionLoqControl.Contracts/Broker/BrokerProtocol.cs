namespace LegionLoqControl.Contracts.Broker;

public static class BrokerProtocol
{
    public const ushort MajorVersion = 1;
    public const int MaximumMessageBytes = 64 * 1024;
}

public sealed record CommandId
{
    public CommandId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Command IDs cannot be empty.", nameof(value));

        Value = value;
    }

    public Guid Value { get; }

    public static CommandId New() => new(Guid.NewGuid());
}
