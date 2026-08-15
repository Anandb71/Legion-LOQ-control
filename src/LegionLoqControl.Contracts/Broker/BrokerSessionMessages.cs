namespace LegionLoqControl.Contracts.Broker;

public enum BrokerSessionKind
{
    Read = 0,
    Write = 1,
    Shutdown = 2,
}

public sealed record BrokerSessionRequest(
    BrokerSessionKind Kind,
    HardwareStateReadRequest? Read,
    HardwareStateWriteRequest? Write);

public sealed record BrokerSessionResponse(
    BrokerSessionKind Kind,
    HardwareStateReadResponse? Read,
    HardwareStateWriteResponse? Write);
