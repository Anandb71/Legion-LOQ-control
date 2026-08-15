namespace LegionLoqControl.Contracts.Broker;

public sealed record HardwareWriteOperation(
    HardwareWriteTarget Target,
    string Expected,
    string Desired);

public sealed record HardwareStateWriteRequest(
    ushort ProtocolMajorVersion,
    Guid RequestId,
    string Nonce,
    int ClientProcessId,
    IReadOnlyList<HardwareWriteOperation> Operations);

public sealed record HardwareStateWriteResponse(
    ushort ProtocolMajorVersion,
    Guid RequestId,
    BrokerCommandStatus Status,
    HardwareStateReadPayload? Snapshot,
    string? ErrorCode);
