namespace LegionLoqControl.Domain.Results;

public enum HardwareReadStatus
{
    Success = 0,
    Unsupported = 1,
    AccessDenied = 2,
    Unavailable = 3,
    InvalidData = 4,
    Failed = 5,
    TimedOut = 6,
}

public sealed record HardwareReadResult<T> where T : struct
{
    private HardwareReadResult(HardwareReadStatus status, T? value, string? errorCode)
    {
        Status = status;
        Value = value;
        ErrorCode = errorCode;
    }

    public HardwareReadStatus Status { get; }

    public T? Value { get; }

    public string? ErrorCode { get; }

    public bool HasValue => Status == HardwareReadStatus.Success && Value.HasValue;

    public static HardwareReadResult<T> Success(T value) =>
        new(HardwareReadStatus.Success, value, null);

    public static HardwareReadResult<T> Failure(HardwareReadStatus status, string errorCode)
    {
        if (status == HardwareReadStatus.Success)
            throw new ArgumentOutOfRangeException(nameof(status), "A failure cannot use Success status.");
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));

        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        return new HardwareReadResult<T>(status, null, errorCode.Trim());
    }
}
