using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Application.Profiles;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Profiles;
using LegionLoqControl.Services;

namespace LegionLoqControl.ViewModels;

public sealed record ProfilePreviewItemViewModel(
    string Feature,
    string Current,
    string Desired,
    string Status,
    string Detail,
    DashboardStateKind State);

public sealed record ProfileModeOption<T>(T Value, string Label) where T : struct
{
    public override string ToString() => Label;
}

public sealed partial class ProfileWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly IProfileStore _store;
    private readonly ProfilePreviewService _previewService;
    private readonly MachineSessionViewModel _session;
    private readonly Func<IReadOnlyList<HardwareWritePlanItem>, CancellationToken, ValueTask<HardwareStateSnapshot>>?
        _applyAsync;
    private ProfileId _draftId = ProfileId.New();
    private bool _initialized;
    private bool _isHydratingDraft;
    private bool _disposed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveDraftCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteDraftCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewDraftCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyProfileCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteDraftCommand))]
    private HardwareProfile? _selectedProfile;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveDraftCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewDraftCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyProfileCommand))]
    private string _draftName = "New profile";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveDraftCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewDraftCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyProfileCommand))]
    private bool _includeBattery = true;

    [ObservableProperty]
    private BatteryChargeMode _selectedBatteryMode = BatteryChargeMode.Conservation;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveDraftCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewDraftCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyProfileCommand))]
    private bool _includeThermal = true;

    [ObservableProperty]
    private ThermalMode _selectedThermalMode = ThermalMode.Balanced;

    [ObservableProperty]
    private string _workspaceTitle = "Preview, then apply";

    [ObservableProperty]
    private string _workspaceMessage =
        "Drafts are local plans. Apply uses the session broker only for would-change targets.";

    [ObservableProperty]
    private DashboardStateKind _workspaceState = DashboardStateKind.Warning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyProfileCommand))]
    private ProfilePreview? _lastPreview;

    public ProfileWorkspaceViewModel(
        IProfileStore store,
        ProfilePreviewService previewService,
        MachineSessionViewModel session,
        Func<IReadOnlyList<HardwareWritePlanItem>, CancellationToken, ValueTask<HardwareStateSnapshot>>?
            applyAsync = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _applyAsync = applyAsync;
        _session.PropertyChanged += Session_PropertyChanged;
    }

    public ObservableCollection<HardwareProfile> Profiles { get; } = [];

    public ObservableCollection<ProfilePreviewItemViewModel> PreviewItems { get; } = [];

    public IReadOnlyList<ProfileModeOption<BatteryChargeMode>> BatteryModeOptions { get; } =
    [
        new(BatteryChargeMode.Normal, "Normal"),
        new(BatteryChargeMode.Conservation, "Conservation"),
        new(BatteryChargeMode.RapidCharge, "Rapid charge"),
    ];

    public IReadOnlyList<ProfileModeOption<ThermalMode>> ThermalModeOptions { get; } =
    [
        new(ThermalMode.Quiet, "Quiet"),
        new(ThermalMode.Balanced, "Balanced"),
        new(ThermalMode.Performance, "Performance"),
        new(ThermalMode.Extreme, "Extreme"),
        new(ThermalMode.Custom, "Custom"),
    ];

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _initialized = true;
        IsBusy = true;
        try
        {
            IReadOnlyList<HardwareProfile> profiles = await _store
                .LoadAsync()
                .ConfigureAwait(true);
            ReplaceProfiles(profiles);
            HydrateDraft(Profiles.FirstOrDefault());
            WorkspaceTitle = profiles.Count == 0
                ? "Create a local draft"
                : "Profile drafts loaded";
            WorkspaceMessage =
                $"{profiles.Count} local draft{(profiles.Count == 1 ? string.Empty : "s")} · preview compares typed state · Apply uses the session broker";
            WorkspaceState = DashboardStateKind.Warning;
        }
        catch (ProfileStoreException exception)
        {
            ReplaceProfiles([]);
            HydrateDraft(profile: null);
            ApplyStoreFailure(exception.ErrorCode);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _session.PropertyChanged -= Session_PropertyChanged;
        _disposed = true;
    }

    partial void OnSelectedProfileChanged(HardwareProfile? value)
    {
        if (!_isHydratingDraft)
            HydrateDraft(value);
    }

    partial void OnDraftNameChanged(string value) => MarkDraftChanged();

    partial void OnIncludeBatteryChanged(bool value) => MarkDraftChanged();

    partial void OnSelectedBatteryModeChanged(BatteryChargeMode value) => MarkDraftChanged();

    partial void OnIncludeThermalChanged(bool value) => MarkDraftChanged();

    partial void OnSelectedThermalModeChanged(ThermalMode value) => MarkDraftChanged();

    [RelayCommand]
    private void NewDraft()
    {
        HydrateDraft(profile: null);
        WorkspaceTitle = "New local draft";
        WorkspaceMessage =
            "Choose bounded targets, then preview or apply. Apply uses the session broker.";
        WorkspaceState = DashboardStateKind.Warning;
    }

    [RelayCommand(CanExecute = nameof(CanSaveDraft))]
    private async Task SaveDraftAsync()
    {
        HardwareProfile profile;
        try
        {
            profile = BuildDraft();
        }
        catch (ArgumentException)
        {
            ApplyInvalidDraft();
            return;
        }

        IsBusy = true;
        try
        {
            await _store.SaveAsync(profile).ConfigureAwait(true);
            UpsertProfile(profile);
            HydrateDraft(profile);
            WorkspaceTitle = "Draft saved locally";
            WorkspaceMessage = "The profile file changed. Hardware state did not.";
            WorkspaceState = DashboardStateKind.Success;
        }
        catch (ProfileStoreException exception)
        {
            ApplyStoreFailure(exception.ErrorCode);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteDraft))]
    private async Task DeleteDraftAsync()
    {
        HardwareProfile profile = SelectedProfile!;
        IsBusy = true;
        try
        {
            bool deleted = await _store
                .DeleteAsync(profile.Id)
                .ConfigureAwait(true);
            Profiles.Remove(profile);

            HydrateDraft(Profiles.FirstOrDefault());
            WorkspaceTitle = deleted ? "Draft deleted" : "Draft already absent";
            WorkspaceMessage = "Only the local profile file changed. No hardware action ran.";
            WorkspaceState = DashboardStateKind.Warning;
        }
        catch (ProfileStoreException exception)
        {
            ApplyStoreFailure(exception.ErrorCode);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanPreviewDraft))]
    private void PreviewDraft()
    {
        try
        {
            ApplyPreview(_previewService.Create(
                BuildDraft(),
                _session.HardwareStateSnapshot,
                _session.MachineSnapshot?.Capabilities ?? []));
        }
        catch (ArgumentException)
        {
            ApplyInvalidDraft();
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyProfile))]
    private async Task ApplyProfileAsync()
    {
        if (_applyAsync is null)
            return;

        ProfilePreview preview;
        IReadOnlyList<HardwareWritePlanItem> operations;
        try
        {
            preview = _previewService.Create(
                BuildDraft(),
                _session.HardwareStateSnapshot,
                _session.MachineSnapshot?.Capabilities ?? []);
            ApplyPreview(preview);
            operations = ProfileApplyPlanner.Plan(preview);
        }
        catch (ArgumentException)
        {
            ApplyInvalidDraft();
            return;
        }
        catch (HardwareWriteException exception)
        {
            ApplyWriteFailure(exception.ErrorCode);
            return;
        }

        if (operations.Count == 0)
        {
            WorkspaceTitle = "Draft already matches";
            WorkspaceMessage = "No hardware write was requested.";
            WorkspaceState = DashboardStateKind.Success;
            return;
        }

        IsBusy = true;
        try
        {
            HardwareStateSnapshot snapshot = await _applyAsync(
                    operations,
                    CancellationToken.None)
                .ConfigureAwait(true);
            _session.UpdateHardwareStateSnapshot(snapshot);
            ApplyPreview(_previewService.Create(
                BuildDraft(),
                snapshot,
                _session.MachineSnapshot?.Capabilities ?? []));
            WorkspaceTitle = "Profile applied";
            WorkspaceMessage = operations.Count == 1
                ? "The session broker applied one would-change target and read it back."
                : "The session broker applied the would-change targets and read them back.";
            WorkspaceState = DashboardStateKind.Success;
        }
        catch (HardwareWriteException exception)
        {
            ApplyWriteFailure(exception.ErrorCode);
        }
        catch (DashboardDataSourceException exception)
        {
            ApplyWriteFailure(exception.ErrorCode);
        }
        catch (Exception)
        {
            ApplyWriteFailure("broker_write_failed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSaveDraft() => !IsBusy && HasValidDraftShape();

    private bool CanDeleteDraft() => !IsBusy && SelectedProfile is not null;

    private bool CanPreviewDraft() => !IsBusy && HasValidDraftShape();

    private bool CanApplyProfile()
    {
        if (IsBusy || _applyAsync is null || !HasValidDraftShape())
            return false;

        try
        {
            ProfilePreview preview = LastPreview ?? _previewService.Create(
                BuildDraft(),
                _session.HardwareStateSnapshot,
                _session.MachineSnapshot?.Capabilities ?? []);
            return ProfileApplyPlanner.Plan(preview).Count > 0;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (HardwareWriteException)
        {
            return false;
        }
    }

    private bool HasValidDraftShape()
    {
        string name = DraftName?.Trim() ?? string.Empty;
        return name.Length is > 0 and <= HardwareProfile.MaximumNameLength
            && !name.Any(char.IsControl)
            && (IncludeBattery || IncludeThermal);
    }

    private HardwareProfile BuildDraft() =>
        new(
            _draftId,
            DraftName,
            new HardwareProfileTargets(
                IncludeBattery ? SelectedBatteryMode : null,
                IncludeThermal ? SelectedThermalMode : null));

    private void HydrateDraft(HardwareProfile? profile)
    {
        _isHydratingDraft = true;
        try
        {
            SelectedProfile = profile;
            _draftId = profile?.Id ?? ProfileId.New();
            DraftName = profile?.Name ?? "New profile";
            IncludeBattery = profile?.Targets.BatteryChargeMode.HasValue ?? true;
            SelectedBatteryMode =
                profile?.Targets.BatteryChargeMode ?? BatteryChargeMode.Conservation;
            IncludeThermal = profile?.Targets.ThermalMode.HasValue ?? true;
            SelectedThermalMode =
                profile?.Targets.ThermalMode ?? ThermalMode.Balanced;
        }
        finally
        {
            _isHydratingDraft = false;
        }

        PreviewCurrentDraft();
    }

    private void PreviewCurrentDraft()
    {
        if (!HasValidDraftShape())
        {
            ApplyInvalidDraft();
            return;
        }

        ApplyPreview(_previewService.Create(
            BuildDraft(),
            _session.HardwareStateSnapshot,
            _session.MachineSnapshot?.Capabilities ?? []));
    }

    private void ApplyPreview(ProfilePreview preview)
    {
        LastPreview = preview;
        PreviewItems.Clear();
        var states = new List<ProfileTargetPreviewState>();
        if (preview.BatteryChargeMode is not null)
        {
            PreviewItems.Add(MapPreview(
                "Battery charge mode",
                preview.BatteryChargeMode,
                FormatBatteryMode));
            states.Add(preview.BatteryChargeMode.State);
        }

        if (preview.ThermalMode is not null)
        {
            PreviewItems.Add(MapPreview(
                "Thermal mode",
                preview.ThermalMode,
                FormatThermalMode));
            states.Add(preview.ThermalMode.State);
        }

        (WorkspaceTitle, WorkspaceMessage, WorkspaceState) = SummarizePreview(states);
    }

    private static ProfilePreviewItemViewModel MapPreview<T>(
        string feature,
        ProfileTargetPreview<T> preview,
        Func<T, string> formatter)
        where T : struct
    {
        string current = preview.Current.HasValue
            ? formatter(preview.Current.Value)
            : "Not read";
        (string status, string detail, DashboardStateKind state) = preview.State switch
        {
            ProfileTargetPreviewState.Matches =>
                ("ALREADY MATCHES", "No change would be required", DashboardStateKind.Success),
            ProfileTargetPreviewState.WouldChange =>
                ("WOULD CHANGE", "The session broker applies this target in one batch", DashboardStateKind.Warning),
            ProfileTargetPreviewState.Stale =>
                ("REFRESH REQUIRED", preview.ReasonCode!, DashboardStateKind.Warning),
            ProfileTargetPreviewState.Unverified =>
                ("UNVERIFIED", preview.ReasonCode!, DashboardStateKind.Warning),
            _ => ("UNAVAILABLE", preview.ReasonCode!, DashboardStateKind.Unavailable),
        };
        return new ProfilePreviewItemViewModel(
            feature,
            current,
            formatter(preview.Desired),
            status,
            detail,
            state);
    }

    private static (
        string Title,
        string Message,
        DashboardStateKind State) SummarizePreview(
            IReadOnlyCollection<ProfileTargetPreviewState> states)
    {
        if (states.All(state => state == ProfileTargetPreviewState.Matches))
        {
            return (
                "Draft already matches",
                "All selected targets match the latest verified state. Apply stays idle.",
                DashboardStateKind.Success);
        }

        if (states.Contains(ProfileTargetPreviewState.Stale))
        {
            return (
                "Hardware refresh required",
                "The last typed snapshot is stale. Refresh it from the dashboard before trusting this preview.",
                DashboardStateKind.Warning);
        }

        if (states.Contains(ProfileTargetPreviewState.Unavailable))
        {
            return (
                "Preview incomplete",
                "At least one current value is unavailable. No hardware action is available.",
                DashboardStateKind.Unavailable);
        }

        if (states.Contains(ProfileTargetPreviewState.Unverified))
        {
            return (
                "Capability evidence incomplete",
                "The draft is valid, but this machine/BIOS is not verified for every selected change.",
                DashboardStateKind.Warning);
        }

        return (
            "Changes previewed",
            "Apply uses the session broker for the would-change targets.",
            DashboardStateKind.Warning);
    }

    private void MarkDraftChanged()
    {
        if (_isHydratingDraft)
            return;

        LastPreview = null;
        PreviewItems.Clear();
        WorkspaceTitle = "Draft changed";
        WorkspaceMessage = "Select Preview draft to compare it with the latest typed snapshot.";
        WorkspaceState = DashboardStateKind.Pending;
    }

    private void ApplyInvalidDraft()
    {
        LastPreview = null;
        PreviewItems.Clear();
        WorkspaceTitle = "Draft needs attention";
        WorkspaceMessage =
            $"Use a name up to {HardwareProfile.MaximumNameLength} characters and select at least one target.";
        WorkspaceState = DashboardStateKind.Warning;
    }

    private void ApplyWriteFailure(string errorCode)
    {
        WorkspaceTitle = errorCode == "broker_elevation_cancelled"
            ? "Elevation cancelled"
            : errorCode == "write_in_progress"
                ? "Write already running"
                : "Profile apply blocked";
        WorkspaceMessage = errorCode switch
        {
            "broker_elevation_cancelled" =>
                "Windows approval was cancelled. No profile write ran.",
            "write_in_progress" =>
                "Another hardware write is already running. Wait, then apply again.",
            "profile_apply_blocked" =>
                "The preview is blocked, so no hardware write ran.",
            "thermal_custom_unsupported" =>
                "Custom thermal mode is not writable from this app.",
            "thermal_expected_mismatch" or "battery_expected_mismatch" =>
                "The live firmware read failed, so this write did not run. Try again.",
            "thermal_readback_mismatch" or "battery_readback_mismatch" =>
                "The setter ran, but readback did not match the requested value.",
            _ => "The broker could not apply this profile without a verified readback.",
        };
        WorkspaceState = errorCode is "broker_elevation_cancelled" or "write_in_progress"
            ? DashboardStateKind.Warning
            : DashboardStateKind.Error;
    }

    private void ApplyStoreFailure(string errorCode)
    {
        LastPreview = null;
        PreviewItems.Clear();
        WorkspaceTitle = "Local profile storage unavailable";
        WorkspaceMessage = errorCode switch
        {
            "profile_store_invalid_json" or "profile_store_invalid_document" =>
                "The profile file is invalid and was not overwritten.",
            "profile_store_schema_unsupported" =>
                "The profile file uses a schema this build does not understand.",
            "profile_store_access_denied" =>
                "Windows denied access to the local profile file.",
            "profile_store_busy" =>
                "Another app instance is using the profile file. Try again in a moment.",
            "profile_store_file_too_large" or "profile_store_limit_exceeded" =>
                "The bounded profile store rejected this file.",
            _ => "The local profile operation failed without changing hardware.",
        };
        WorkspaceState = errorCode == "profile_store_busy"
            ? DashboardStateKind.Warning
            : DashboardStateKind.Error;
    }

    private void ReplaceProfiles(IEnumerable<HardwareProfile> profiles)
    {
        Profiles.Clear();
        foreach (HardwareProfile profile in profiles)
            Profiles.Add(profile);
    }

    private void UpsertProfile(HardwareProfile profile)
    {
        HardwareProfile? existing = Profiles.FirstOrDefault(item => item.Id == profile.Id);
        if (existing is not null)
            Profiles.Remove(existing);

        int index = 0;
        while (index < Profiles.Count
            && StringComparer.OrdinalIgnoreCase.Compare(Profiles[index].Name, profile.Name) <= 0)
        {
            index++;
        }

        Profiles.Insert(index, profile);
    }

    private void Session_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MachineSessionViewModel.MachineSnapshot)
            or nameof(MachineSessionViewModel.HardwareStateSnapshot))
        {
            PreviewCurrentDraft();
        }
    }

    private static string FormatBatteryMode(BatteryChargeMode value) =>
        value switch
        {
            BatteryChargeMode.RapidCharge => "Rapid charge",
            BatteryChargeMode.Conservation => "Conservation",
            _ => "Normal",
        };

    private static string FormatThermalMode(ThermalMode value) =>
        value switch
        {
            ThermalMode.Quiet => "Quiet",
            ThermalMode.Balanced => "Balanced",
            ThermalMode.Performance => "Performance",
            ThermalMode.Extreme => "Extreme",
            ThermalMode.Custom => "Custom",
            _ => value.ToString(),
        };
}

