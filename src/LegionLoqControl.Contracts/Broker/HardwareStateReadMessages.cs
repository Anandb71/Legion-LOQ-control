using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;

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
    HardwareStateReadPayload? Snapshot,
    string? ErrorCode);

public sealed record HardwareStateReadPayload(
    DateTimeOffset ObservedAt,
    HardwareReadValue<BatteryChargeMode> BatteryChargeMode,
    HardwareReadValue<ThermalMode> ThermalMode,
    HardwareReadValue<ToggleState> DisplayOverdrive,
    HardwareReadValue<IntegratedGpuMode> IntegratedGpuMode,
    HardwareReadValue<FourZoneKeyboardMode> FourZoneKeyboard)
{
    public static HardwareStateReadPayload FromSnapshot(HardwareStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new HardwareStateReadPayload(
            snapshot.ObservedAt,
            HardwareReadValue<BatteryChargeMode>.FromResult(snapshot.BatteryChargeMode),
            HardwareReadValue<ThermalMode>.FromResult(snapshot.ThermalMode),
            HardwareReadValue<ToggleState>.FromResult(snapshot.DisplayOverdrive),
            HardwareReadValue<IntegratedGpuMode>.FromResult(snapshot.IntegratedGpuMode),
            HardwareReadValue<FourZoneKeyboardMode>.FromResult(snapshot.FourZoneKeyboard));
    }

    public HardwareStateSnapshot ToSnapshot()
    {
        if (BatteryChargeMode is null ||
            ThermalMode is null ||
            DisplayOverdrive is null ||
            IntegratedGpuMode is null ||
            FourZoneKeyboard is null)
        {
            throw new InvalidDataException("The hardware state payload is incomplete.");
        }

        return new HardwareStateSnapshot(
            ObservedAt,
            BatteryChargeMode.ToResult(),
            ThermalMode.ToResult(),
            DisplayOverdrive.ToResult(),
            IntegratedGpuMode.ToResult(),
            FourZoneKeyboard.ToResult());
    }
}

public sealed record HardwareReadValue<T>(
    HardwareReadStatus Status,
    T? Value,
    string? ErrorCode)
    where T : struct
{
    public static HardwareReadValue<T> FromResult(HardwareReadResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new HardwareReadValue<T>(result.Status, result.Value, result.ErrorCode);
    }

    public HardwareReadResult<T> ToResult()
    {
        if (!Enum.IsDefined(Status))
            throw new InvalidDataException("The hardware read status is invalid.");

        if (Status == HardwareReadStatus.Success)
        {
            if (!Value.HasValue || ErrorCode is not null)
                throw new InvalidDataException("A successful hardware read has an invalid shape.");
            return HardwareReadResult<T>.Success(Value.Value);
        }

        if (Value.HasValue || !IsValidErrorCode(ErrorCode))
            throw new InvalidDataException("A failed hardware read has an invalid shape.");
        return HardwareReadResult<T>.Failure(Status, ErrorCode!);
    }

    private static bool IsValidErrorCode(string? value) =>
        value is { Length: > 0 and <= 64 } &&
        value.All(static character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
}
