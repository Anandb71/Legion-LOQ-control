using LegionLoqControl.Domain.Automation;
using LegionLoqControl.Domain.Profiles;

namespace LegionLoqControl.Application.Automation;

public enum AutomationRunVerdict
{
    Apply = 0,
    SkipUnchanged = 1,
    SkipCooldown = 2,
    SkipBlocked = 3,
    Suspended = 4,
}

public sealed class AutomationRunService
{
    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(2);

    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _cooldown;
    private bool _suspended;
    private string? _suspendReason;
    private DateTimeOffset? _cooldownUntilUtc;
    private ProfileId? _lastAppliedProfileId;
    private PowerSourceKind? _lastAppliedPowerSource;

    public AutomationRunService(
        TimeProvider? timeProvider = null,
        TimeSpan? cooldown = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _cooldown = cooldown ?? DefaultCooldown;
        if (_cooldown <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(cooldown));
    }

    public bool IsSuspended => _suspended;

    public string? SuspendReason => _suspendReason;

    public DateTimeOffset? CooldownUntilUtc => _cooldownUntilUtc;

    public ProfileId? LastAppliedProfileId => _lastAppliedProfileId;

    public PowerSourceKind? LastAppliedPowerSource => _lastAppliedPowerSource;

    public AutomationRunVerdict Evaluate(
        ProfileId profileId,
        PowerSourceKind powerSource,
        bool hasWouldChangeOperations)
    {
        if (profileId.Value == Guid.Empty)
            throw new ArgumentException("A target profile ID cannot be empty.", nameof(profileId));
        if (!Enum.IsDefined(powerSource))
            throw new ArgumentOutOfRangeException(nameof(powerSource));

        if (_suspended)
            return AutomationRunVerdict.Suspended;

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (_cooldownUntilUtc is { } until && now < until)
            return AutomationRunVerdict.SkipCooldown;

        if (!hasWouldChangeOperations)
        {
            RememberApplied(profileId, powerSource);
            return AutomationRunVerdict.SkipUnchanged;
        }

        if (_lastAppliedProfileId == profileId && _lastAppliedPowerSource == powerSource)
            return AutomationRunVerdict.SkipUnchanged;

        return AutomationRunVerdict.Apply;
    }

    public void NoteSuccess(ProfileId profileId, PowerSourceKind powerSource)
    {
        RememberApplied(profileId, powerSource);
        BeginCooldown();
    }

    public void NoteCancel() => BeginCooldown();

    public void NoteBlocked() => BeginCooldown();

    public void NoteFailure(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        BeginCooldown();
        if (!ShouldSuspend(errorCode.Trim()))
            return;

        _suspended = true;
        _suspendReason = errorCode.Trim();
    }

    public void Resume()
    {
        _suspended = false;
        _suspendReason = null;
    }

    public void Reset()
    {
        _suspended = false;
        _suspendReason = null;
        _cooldownUntilUtc = null;
        _lastAppliedProfileId = null;
        _lastAppliedPowerSource = null;
    }

    private void RememberApplied(ProfileId profileId, PowerSourceKind powerSource)
    {
        _lastAppliedProfileId = profileId;
        _lastAppliedPowerSource = powerSource;
    }

    private void BeginCooldown() =>
        _cooldownUntilUtc = _timeProvider.GetUtcNow() + _cooldown;

    private static bool ShouldSuspend(string errorCode) =>
        errorCode.EndsWith("_readback_mismatch", StringComparison.Ordinal) ||
        errorCode is "broker_write_failed"
            or "broker_internal_failed"
            or "broker_io_failed";
}
