using LegionLoqControl.Domain.Diagnostics;

namespace LegionLoqControl.Contracts.Broker;

public enum BrokerReadStatus
{
    Succeeded = 0,
    InvalidRequest = 1,
    VersionMismatch = 2,
    Unauthorized = 3,
    Failed = 4,
}

public sealed record HardwareStateReadRequest(
    ushort ProtocolMajorVersion,
    Guid RequestId,
    string Nonce,
    int ClientProcessId);

public sealed record HardwareStateReadResponse(
    ushort ProtocolMajorVersion,
    Guid RequestId,
    BrokerReadStatus Status,
    HardwareStateSnapshot? Snapshot,
    string? ErrorCode);
