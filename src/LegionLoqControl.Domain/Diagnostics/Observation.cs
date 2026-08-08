namespace LegionLoqControl.Domain.Diagnostics;

public enum ObservationState
{
    Observed = 0,
    Unavailable = 1,
    Failed = 2,
}

public sealed record Observation
{
    private Observation(ObservationState state, string? value, string? errorCode, string? detail)
    {
        State = state;
        Value = value;
        ErrorCode = errorCode;
        Detail = detail;
    }

    public ObservationState State { get; }

    public string? Value { get; }

    public string? ErrorCode { get; }

    public string? Detail { get; }

    public static Observation FromValue(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new Observation(ObservationState.Observed, value.Trim(), null, null);
    }

    public static Observation Unavailable(string errorCode, string? detail = null) =>
        FromError(ObservationState.Unavailable, errorCode, detail);

    public static Observation Failed(string errorCode, string? detail = null) =>
        FromError(ObservationState.Failed, errorCode, detail);

    private static Observation FromError(ObservationState state, string errorCode, string? detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        return new Observation(state, null, errorCode.Trim(), detail?.Trim());
    }
}
