using LegionLoqControl.Contracts.Broker;

namespace LegionLoqControl.Infrastructure.Windows.Broker;

internal static class BrokerMessageValidator
{
    public static BrokerValidationResult ValidateRequest(
        HardwareStateReadRequest? request,
        string expectedNonce,
        int expectedClientProcessId)
    {
        if (!BrokerProtocol.IsValidNonce(expectedNonce))
            throw new ArgumentException("The expected nonce is invalid.", nameof(expectedNonce));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedClientProcessId);

        if (request is null)
            return BrokerValidationResult.Invalid("request_missing");
        if (request.ProtocolMajorVersion != BrokerProtocol.MajorVersion)
            return new(BrokerReadStatus.VersionMismatch, "protocol_version_mismatch");
        if (request.RequestId == Guid.Empty)
            return BrokerValidationResult.Invalid("request_id_empty");
        if (request.ClientProcessId <= 0)
            return BrokerValidationResult.Invalid("client_process_id_invalid");
        if (!BrokerProtocol.IsValidNonce(request.Nonce))
            return BrokerValidationResult.Invalid("nonce_invalid");
        if (request.ClientProcessId != expectedClientProcessId)
            return new(BrokerReadStatus.Unauthorized, "client_process_mismatch");
        if (!BrokerProtocol.NoncesEqual(request.Nonce, expectedNonce))
            return new(BrokerReadStatus.Unauthorized, "nonce_mismatch");

        return BrokerValidationResult.Valid;
    }

    public static BrokerValidationResult ValidateSessionRequest(
        BrokerSessionRequest? request,
        string expectedNonce,
        int expectedClientProcessId)
    {
        if (request is null)
            return BrokerValidationResult.Invalid("request_missing");
        if (!Enum.IsDefined(request.Kind))
            return BrokerValidationResult.Invalid("session_kind_invalid");

        if (request.Kind == BrokerSessionKind.Write)
        {
            if (request.Write is null || request.Read is not null)
                return BrokerValidationResult.Invalid("write_request_missing");
            return ValidateWriteRequest(request.Write, expectedNonce, expectedClientProcessId);
        }

        if (request.Read is null || request.Write is not null)
            return BrokerValidationResult.Invalid("request_missing");
        return ValidateRequest(request.Read, expectedNonce, expectedClientProcessId);
    }

    public static void ValidateSessionResponse(
        BrokerSessionResponse response,
        BrokerSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(request);
        if (response.Kind != request.Kind || !Enum.IsDefined(response.Kind))
            throw new InvalidDataException("The broker session response kind does not match.");

        if (request.Kind == BrokerSessionKind.Write)
        {
            if (response.Write is null || response.Read is not null || request.Write is null)
                throw new InvalidDataException("A write session response has an invalid shape.");
            ValidateWriteResponse(response.Write, request.Write.RequestId);
            return;
        }

        if (response.Read is null || response.Write is not null || request.Read is null)
            throw new InvalidDataException("A read session response has an invalid shape.");
        ValidateResponse(response.Read, request.Read.RequestId);
    }

    public static BrokerValidationResult ValidateWriteRequest(
        HardwareStateWriteRequest? request,
        string expectedNonce,
        int expectedClientProcessId)
    {
        if (request is null)
            return BrokerValidationResult.Invalid("request_missing");

        BrokerValidationResult identity = ValidateRequest(
            new HardwareStateReadRequest(
                request.ProtocolMajorVersion,
                request.RequestId,
                request.Nonce,
                request.ClientProcessId),
            expectedNonce,
            expectedClientProcessId);
        if (!identity.IsValid)
            return identity;
        if (request.Operations is not { Count: >= 1 and <= 2 })
            return BrokerValidationResult.Invalid("write_batch_invalid");

        HashSet<HardwareWriteTarget> targets = [];
        foreach (HardwareWriteOperation operation in request.Operations)
        {
            if (!Enum.IsDefined(operation.Target) ||
                string.IsNullOrWhiteSpace(operation.Expected) ||
                string.IsNullOrWhiteSpace(operation.Desired) ||
                operation.Expected.Length > 64 ||
                operation.Desired.Length > 64 ||
                !targets.Add(operation.Target))
            {
                return BrokerValidationResult.Invalid("write_batch_invalid");
            }
        }

        return BrokerValidationResult.Valid;
    }

    public static void ValidateWriteResponse(
        HardwareStateWriteResponse response,
        Guid expectedRequestId)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (expectedRequestId == Guid.Empty)
            throw new ArgumentException("The expected request ID cannot be empty.", nameof(expectedRequestId));
        if (response.ProtocolMajorVersion != BrokerProtocol.MajorVersion)
            throw new InvalidDataException("The broker response protocol version does not match.");
        if (response.RequestId != expectedRequestId)
            throw new InvalidDataException("The broker response request ID does not match.");
        if (!Enum.IsDefined(response.Status))
            throw new InvalidDataException("The broker response status is invalid.");

        if (response.Status == BrokerCommandStatus.Succeeded)
        {
            if (response.Snapshot is null || response.ErrorCode is not null)
                throw new InvalidDataException("A successful write response has an invalid shape.");
            _ = response.Snapshot.ToSnapshot();
            return;
        }

        if (response.Snapshot is not null || !IsValidErrorCode(response.ErrorCode))
            throw new InvalidDataException("A failed write response has an invalid shape.");
    }

    public static void ValidateResponse(
        HardwareStateReadResponse response,
        Guid expectedRequestId)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (expectedRequestId == Guid.Empty)
            throw new ArgumentException("The expected request ID cannot be empty.", nameof(expectedRequestId));
        if (response.ProtocolMajorVersion != BrokerProtocol.MajorVersion)
            throw new InvalidDataException("The broker response protocol version does not match.");
        if (response.RequestId != expectedRequestId)
            throw new InvalidDataException("The broker response request ID does not match.");
        if (!Enum.IsDefined(response.Status))
            throw new InvalidDataException("The broker response status is invalid.");

        if (response.Status == BrokerReadStatus.Succeeded)
        {
            if (response.Snapshot is null || response.ErrorCode is not null)
                throw new InvalidDataException("A successful broker response has an invalid shape.");
            _ = response.Snapshot.ToSnapshot();
            return;
        }

        if (response.Snapshot is not null || !IsValidErrorCode(response.ErrorCode))
            throw new InvalidDataException("A failed broker response has an invalid shape.");
    }

    private static bool IsValidErrorCode(string? value) =>
        value is { Length: > 0 and <= 64 } &&
        value.All(static character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
}

internal readonly record struct BrokerValidationResult(
    BrokerReadStatus Status,
    string? ErrorCode)
{
    public static BrokerValidationResult Valid { get; } =
        new(BrokerReadStatus.Succeeded, null);

    public bool IsValid => Status == BrokerReadStatus.Succeeded;

    public static BrokerValidationResult Invalid(string errorCode) =>
        new(BrokerReadStatus.InvalidRequest, errorCode);
}
