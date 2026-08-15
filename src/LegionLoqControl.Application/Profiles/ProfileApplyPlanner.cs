using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Profiles;

namespace LegionLoqControl.Application.Profiles;

public sealed record HardwareWritePlanItem(
    HardwareWriteKind Kind,
    string Expected,
    string Desired);

public static class ProfileApplyPlanner
{
    public const int MaximumOperations = 2;

    public static IReadOnlyList<HardwareWritePlanItem> Plan(ProfilePreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (IsBlocked(preview.BatteryChargeMode) || IsBlocked(preview.ThermalMode))
        {
            throw new HardwareWriteException(
                "profile_apply_blocked",
                HardwareWriteStatus.Conflict);
        }

        if (preview.ThermalMode is { State: ProfileTargetPreviewState.WouldChange } thermal &&
            thermal.Desired == ThermalMode.Custom)
        {
            throw new HardwareWriteException(
                "thermal_custom_unsupported",
                HardwareWriteStatus.Unsupported);
        }

        List<HardwareWritePlanItem> operations = [];
        if (preview.BatteryChargeMode is { State: ProfileTargetPreviewState.WouldChange } battery &&
            battery.Current.HasValue)
        {
            operations.Add(new HardwareWritePlanItem(
                HardwareWriteKind.BatteryChargeMode,
                battery.Current.Value.ToString(),
                battery.Desired.ToString()));
        }

        if (preview.ThermalMode is { State: ProfileTargetPreviewState.WouldChange } change &&
            change.Current.HasValue)
        {
            operations.Add(new HardwareWritePlanItem(
                HardwareWriteKind.ThermalMode,
                change.Current.Value.ToString(),
                change.Desired.ToString()));
        }

        if (operations.Count > MaximumOperations)
        {
            throw new HardwareWriteException(
                "profile_apply_too_many",
                HardwareWriteStatus.Failed);
        }

        return operations;
    }

    private static bool IsBlocked<T>(ProfileTargetPreview<T>? preview)
        where T : struct =>
        preview?.State is
            ProfileTargetPreviewState.Unavailable
            or ProfileTargetPreviewState.Unverified
            or ProfileTargetPreviewState.Stale;
}
