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
        if (!Enum.IsDefined(request.Target))
            return BrokerValidationResult.Invalid("write_target_invalid");
        if (string.IsNullOrWhiteSpace(request.Expected) ||
            string.IsNullOrWhiteSpace(request.Desired))
        {
            return BrokerValidationResult.Invalid("write_value_invalid");
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
