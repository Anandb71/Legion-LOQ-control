using LegionLoqControl.Domain.Controls;

namespace LegionLoqControl.Domain.Profiles;

public enum ProfileTargetPreviewState
{
    Matches = 0,
    WouldChange = 1,
    Unavailable = 2,
    Unverified = 3,
    Stale = 4,
}

public sealed record ProfileTargetPreview<T> where T : struct
{
    private ProfileTargetPreview(
        ProfileTargetPreviewState state,
        T desired,
        T? current,
        string? reasonCode)
    {
        State = state;
        Desired = desired;
        Current = current;
        ReasonCode = reasonCode;
    }

    public ProfileTargetPreviewState State { get; }

    public T Desired { get; }

    public T? Current { get; }

    public string? ReasonCode { get; }

    public static ProfileTargetPreview<T> Matches(T value) =>
        new(ProfileTargetPreviewState.Matches, value, value, null);

    public static ProfileTargetPreview<T> WouldChange(T current, T desired)
    {
        if (EqualityComparer<T>.Default.Equals(current, desired))
            throw new ArgumentException("A change preview requires different current and desired values.");

        return new ProfileTargetPreview<T>(
            ProfileTargetPreviewState.WouldChange,
            desired,
            current,
            null);
    }

    public static ProfileTargetPreview<T> Blocked(
        ProfileTargetPreviewState state,
        T desired,
        T? current,
        string reasonCode)
    {
        if (state is not (
            ProfileTargetPreviewState.Unavailable
            or ProfileTargetPreviewState.Unverified
            or ProfileTargetPreviewState.Stale))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return new ProfileTargetPreview<T>(state, desired, current, reasonCode.Trim());
    }
}

public sealed record ProfilePreview
{
    public ProfilePreview(
        HardwareProfile profile,
        DateTimeOffset evaluatedAt,
        ProfileTargetPreview<BatteryChargeMode>? batteryChargeMode,
        ProfileTargetPreview<ThermalMode>? thermalMode)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        if (batteryChargeMode is null && thermalMode is null)
            throw new ArgumentException("A profile preview must contain at least one target.");

        EvaluatedAt = evaluatedAt;
        BatteryChargeMode = batteryChargeMode;
        ThermalMode = thermalMode;
    }

    public HardwareProfile Profile { get; }

    public DateTimeOffset EvaluatedAt { get; }

    public ProfileTargetPreview<BatteryChargeMode>? BatteryChargeMode { get; }

    public ProfileTargetPreview<ThermalMode>? ThermalMode { get; }
}

