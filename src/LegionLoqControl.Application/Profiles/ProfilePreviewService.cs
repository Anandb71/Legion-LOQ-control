using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Profiles;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Application.Profiles;

public sealed class ProfilePreviewService
{
    public static readonly TimeSpan DefaultMaximumSnapshotAge = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _maximumSnapshotAge;

    public ProfilePreviewService(
        TimeProvider? timeProvider = null,
        TimeSpan? maximumSnapshotAge = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _maximumSnapshotAge = maximumSnapshotAge ?? DefaultMaximumSnapshotAge;

        if (_maximumSnapshotAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maximumSnapshotAge));
    }

    public ProfilePreview Create(
        HardwareProfile profile,
        HardwareStateSnapshot? snapshot,
        IReadOnlyList<CapabilityEvidence> capabilities)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(capabilities);

        DateTimeOffset evaluatedAt = _timeProvider.GetUtcNow();
        ProfileTargetPreview<BatteryChargeMode>? battery = profile.Targets.BatteryChargeMode is { } batteryTarget
            ? PreviewBattery(batteryTarget, snapshot, capabilities, evaluatedAt)
            : null;
        ProfileTargetPreview<ThermalMode>? thermal = profile.Targets.ThermalMode is { } thermalTarget
            ? PreviewTarget(
                thermalTarget,
                snapshot?.ThermalMode,
                snapshot?.ObservedAt,
                [HardwareCapability.ThermalMode],
                capabilities,
                evaluatedAt)
            : null;

        return new ProfilePreview(profile, evaluatedAt, battery, thermal);
    }

    private ProfileTargetPreview<BatteryChargeMode> PreviewBattery(
        BatteryChargeMode desired,
        HardwareStateSnapshot? snapshot,
        IReadOnlyList<CapabilityEvidence> capabilities,
        DateTimeOffset evaluatedAt)
    {
        BatteryChargeMode? current = snapshot?.BatteryChargeMode.Value;
        HardwareCapability[] requiredCapabilities = (desired, current) switch
        {
            (BatteryChargeMode.Conservation, BatteryChargeMode.RapidCharge)
                or (BatteryChargeMode.RapidCharge, BatteryChargeMode.Conservation) =>
                [HardwareCapability.BatteryConservationMode, HardwareCapability.BatteryRapidCharge],
            (BatteryChargeMode.RapidCharge, _) =>
                [HardwareCapability.BatteryRapidCharge],
            (BatteryChargeMode.Normal, BatteryChargeMode.RapidCharge) =>
                [HardwareCapability.BatteryRapidCharge],
            _ => [HardwareCapability.BatteryConservationMode],
        };

        return PreviewTarget(
            desired,
            snapshot?.BatteryChargeMode,
            snapshot?.ObservedAt,
            requiredCapabilities,
            capabilities,
            evaluatedAt);
    }

    private ProfileTargetPreview<T> PreviewTarget<T>(
        T desired,
        HardwareReadResult<T>? currentResult,
        DateTimeOffset? observedAt,
        IReadOnlyList<HardwareCapability> requiredCapabilities,
        IReadOnlyList<CapabilityEvidence> capabilities,
        DateTimeOffset evaluatedAt)
        where T : struct
    {
        if (currentResult is null || !observedAt.HasValue)
        {
            return ProfileTargetPreview<T>.Blocked(
                ProfileTargetPreviewState.Unavailable,
                desired,
                current: null,
                "hardware_state_not_read");
        }

        if (!currentResult.HasValue)
        {
            return ProfileTargetPreview<T>.Blocked(
                ProfileTargetPreviewState.Unavailable,
                desired,
                current: null,
                currentResult.ErrorCode ?? "hardware_state_unavailable");
        }

        T current = currentResult.Value!.Value;
        string? staleReason = GetStaleReason(observedAt.Value, evaluatedAt);
        if (staleReason is not null)
        {
            return ProfileTargetPreview<T>.Blocked(
                ProfileTargetPreviewState.Stale,
                desired,
                current,
                staleReason);
        }

        if (EqualityComparer<T>.Default.Equals(current, desired))
            return ProfileTargetPreview<T>.Matches(current);

        foreach (HardwareCapability requiredCapability in requiredCapabilities)
        {
            CapabilityEvidence[] evidence = capabilities
                .Where(item => item.Capability == requiredCapability)
                .ToArray();
            if (evidence.Length == 0)
            {
                return ProfileTargetPreview<T>.Blocked(
                    ProfileTargetPreviewState.Unverified,
                    desired,
                    current,
                    "capability_evidence_missing");
            }

            if (evidence.Length != 1)
            {
                return ProfileTargetPreview<T>.Blocked(
                    ProfileTargetPreviewState.Unverified,
                    desired,
                    current,
                    "capability_evidence_ambiguous");
            }

            ProfileTargetPreview<T>? blocked = evidence[0].Support switch
            {
                CapabilitySupport.Supported => null,
                CapabilitySupport.Unsupported => ProfileTargetPreview<T>.Blocked(
                    ProfileTargetPreviewState.Unavailable,
                    desired,
                    current,
                    evidence[0].EvidenceCode),
                CapabilitySupport.Unknown or CapabilitySupport.Degraded => ProfileTargetPreview<T>.Blocked(
                    ProfileTargetPreviewState.Unverified,
                    desired,
                    current,
                    evidence[0].EvidenceCode),
                _ => ProfileTargetPreview<T>.Blocked(
                    ProfileTargetPreviewState.Unverified,
                    desired,
                    current,
                    "capability_evidence_invalid"),
            };
            if (blocked is not null)
                return blocked;
        }

        return ProfileTargetPreview<T>.WouldChange(current, desired);
    }

    private string? GetStaleReason(DateTimeOffset observedAt, DateTimeOffset evaluatedAt)
    {
        if (observedAt > evaluatedAt)
            return "hardware_state_timestamp_invalid";

        return evaluatedAt - observedAt > _maximumSnapshotAge
            ? "hardware_state_stale"
            : null;
    }
}

