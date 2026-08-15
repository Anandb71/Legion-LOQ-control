using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LegionLoqControl.Application.Automation;
using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Application.Profiles;
using LegionLoqControl.Domain.Automation;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Profiles;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Services;

namespace LegionLoqControl.ViewModels;

public sealed record AutomationPowerSourceOption(
    PowerSourceKind Value,
    string Label)
{
    public override string ToString() => Label;
}

public sealed partial class AutomationWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly IAutomationRuleStore _ruleStore;
    private readonly IProfileStore _profileStore;
    private readonly PowerSourceService _powerSourceService;
    private readonly AutomationPreviewService _previewService;
    private readonly MachineSessionViewModel? _session;
    private readonly ProfilePreviewService _profilePreviewService;
    private readonly AutomationRunService _runService;
    private readonly Func<IReadOnlyList<HardwareWritePlanItem>, CancellationToken, ValueTask<HardwareStateSnapshot>>?
        _applyAsync;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _watchCts;
    private AutomationRuleId _draftId = AutomationRuleId.New();
    private PowerSourceSnapshot? _powerSourceSnapshot;
    private bool _initialized;
    private bool _isHydratingDraft;
    private bool _disposed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshPowerSourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartWatchingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopWatchingCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeWatcherCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteRuleCommand))]
    private AutomationRule? _selectedRule;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewRuleCommand))]
    private HardwareProfile? _selectedProfile;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewRuleCommand))]
    private string _draftName = "New rule";

    [ObservableProperty]
    private PowerSourceKind _selectedPowerSource = PowerSourceKind.Ac;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewRuleCommand))]
    private string _draftPriority = "100";

    [ObservableProperty]
    private bool _draftIsEnabled = true;

    [ObservableProperty]
    private string _workspaceTitle = "Preview, then watch";

    [ObservableProperty]
    private string _workspaceMessage =
        "Rules stay local. Start watching to apply the winning profile with one Windows approval per change.";

    [ObservableProperty]
    private DashboardStateKind _workspaceState = DashboardStateKind.Warning;

    [ObservableProperty]
    private string _powerSourceValue = "Not observed";

    [ObservableProperty]
    private string _powerSourceDetail = "Read locally through GetSystemPowerStatus";

    [ObservableProperty]
    private DashboardStateKind _powerSourceState = DashboardStateKind.Pending;

    [ObservableProperty]
    private string _previewStatus = "NOT EVALUATED";

    [ObservableProperty]
    private string _previewRuleName = "—";

    [ObservableProperty]
    private string _previewProfileName = "—";

    [ObservableProperty]
    private string _previewDetail =
        "Observe the power source and preview a valid rule draft.";

    [ObservableProperty]
    private DashboardStateKind _previewState = DashboardStateKind.Pending;

    [ObservableProperty]
    private AutomationPreview? _lastPreview;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartWatchingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopWatchingCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeWatcherCommand))]
    private bool _isWatching;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartWatchingCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeWatcherCommand))]
    private bool _isSuspended;

    [ObservableProperty]
    private string _watcherStatus = "IDLE";

    [ObservableProperty]
    private string _watcherDetail =
        "Watching stays in this app session. Each apply asks Windows for approval.";

    [ObservableProperty]
    private DashboardStateKind _watcherState = DashboardStateKind.Pending;

    public AutomationWorkspaceViewModel(
        IAutomationRuleStore ruleStore,
        IProfileStore profileStore,
        PowerSourceService powerSourceService,
        AutomationPreviewService previewService,
        MachineSessionViewModel? session = null,
        ProfilePreviewService? profilePreviewService = null,
        AutomationRunService? runService = null,
        Func<IReadOnlyList<HardwareWritePlanItem>, CancellationToken, ValueTask<HardwareStateSnapshot>>?
            applyAsync = null,
        TimeProvider? timeProvider = null)
    {
        _ruleStore = ruleStore ?? throw new ArgumentNullException(nameof(ruleStore));
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _powerSourceService =
            powerSourceService ?? throw new ArgumentNullException(nameof(powerSourceService));
        _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
        _session = session;
        _profilePreviewService = profilePreviewService ?? new ProfilePreviewService();
        _runService = runService ?? new AutomationRunService();
        _applyAsync = applyAsync;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ObservableCollection<AutomationRule> Rules { get; } = [];

    public ObservableCollection<HardwareProfile> Profiles { get; } = [];

    public IReadOnlyList<AutomationPowerSourceOption> PowerSourceOptions { get; } =
    [
        new(PowerSourceKind.Ac, "AC power"),
        new(PowerSourceKind.Battery, "Battery power"),
    ];

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _initialized = true;
        IsBusy = true;
        try
        {
            IReadOnlyList<AutomationRule> rules = await _ruleStore
                .LoadAsync(_lifetime.Token)
                .ConfigureAwait(true);
            IReadOnlyList<HardwareProfile> profiles = await _profileStore
                .LoadAsync(_lifetime.Token)
                .ConfigureAwait(true);

            ReplaceRules(rules);
            ReplaceProfiles(profiles);
            HydrateDraft(Rules.FirstOrDefault());
            await CapturePowerSourceAsync(_lifetime.Token).ConfigureAwait(true);
            PreviewCurrentDraft();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (AutomationRuleStoreException exception)
        {
            ReplaceRules([]);
            ReplaceProfiles([]);
            HydrateDraft(rule: null);
            ApplyStoreFailure(exception.ErrorCode, isRuleStore: true);
        }
        catch (ProfileStoreException exception)
        {
            ReplaceRules([]);
            ReplaceProfiles([]);
            HydrateDraft(rule: null);
            ApplyStoreFailure(exception.ErrorCode, isRuleStore: false);
        }
        catch (Exception)
        {
            ReplaceRules([]);
            ReplaceProfiles([]);
            HydrateDraft(rule: null);
            ApplyUnexpectedFailure();
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal void SynchronizeProfiles(IEnumerable<HardwareProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        ProfileId? preferredProfileId =
            SelectedProfile?.Id ?? SelectedRule?.ProfileId;
        ReplaceProfiles(profiles);

        _isHydratingDraft = true;
        try
        {
            SelectedProfile = preferredProfileId.HasValue
                ? Profiles.FirstOrDefault(profile => profile.Id == preferredProfileId.Value)
                : Profiles.FirstOrDefault();
        }
        finally
        {
            _isHydratingDraft = false;
        }

        if (_initialized)
            PreviewCurrentDraft();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _watchCts?.Cancel();
        _watchCts?.Dispose();
        _lifetime.Cancel();
        _lifetime.Dispose();
        _disposed = true;
    }

    partial void OnSelectedRuleChanged(AutomationRule? value)
    {
        if (_isHydratingDraft)
            return;

        HydrateDraft(value);
        PreviewCurrentDraft();
    }

    partial void OnSelectedProfileChanged(HardwareProfile? value) => MarkDraftChanged();

    partial void OnDraftNameChanged(string value) => MarkDraftChanged();

    partial void OnSelectedPowerSourceChanged(PowerSourceKind value) => MarkDraftChanged();

    partial void OnDraftPriorityChanged(string value) => MarkDraftChanged();

    partial void OnDraftIsEnabledChanged(bool value) => MarkDraftChanged();

    [RelayCommand]
    private void NewRule()
    {
        HydrateDraft(rule: null);
        PreviewCurrentDraft();
    }

    [RelayCommand(CanExecute = nameof(CanSaveRule))]
    private async Task SaveRuleAsync()
    {
        AutomationRule rule;
        try
        {
            rule = BuildDraft();
        }
        catch (ArgumentException)
        {
            ApplyInvalidDraft();
            return;
        }

        IsBusy = true;
        try
        {
            await _ruleStore
                .SaveAsync(rule, _lifetime.Token)
                .ConfigureAwait(true);
            UpsertRule(rule);
            HydrateDraft(rule);
            PreviewCurrentDraft();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (AutomationRuleStoreException exception)
        {
            ApplyStoreFailure(exception.ErrorCode, isRuleStore: true);
        }
        catch (Exception)
        {
            ApplyUnexpectedFailure();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteRule))]
    private async Task DeleteRuleAsync()
    {
        AutomationRule rule = SelectedRule!;
        IsBusy = true;
        try
        {
            bool deleted = await _ruleStore
                .DeleteAsync(rule.Id, _lifetime.Token)
                .ConfigureAwait(true);
            Rules.Remove(rule);
            HydrateDraft(Rules.FirstOrDefault());
            PreviewCurrentDraft();

            WorkspaceTitle = deleted ? "Rule deleted" : "Rule already absent";
            WorkspaceMessage =
                "Only the local automation file changed. No profile or hardware action ran.";
            WorkspaceState = DashboardStateKind.Warning;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (AutomationRuleStoreException exception)
        {
            ApplyStoreFailure(exception.ErrorCode, isRuleStore: true);
        }
        catch (Exception)
        {
            ApplyUnexpectedFailure();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanPreviewRule))]
    private void PreviewRule()
    {
        try
        {
            ApplyPreview(CreateDraftPreview(BuildDraft()));
        }
        catch (ArgumentException)
        {
            ApplyInvalidDraft();
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartWatching))]
    private async Task StartWatchingAsync()
    {
        if (_applyAsync is null || IsWatching)
            return;

        _watchCts?.Dispose();
        _watchCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        IsWatching = true;
        WatcherStatus = "WATCHING";
        WatcherDetail = "Observing AC/battery in this session. A power-source change can request Windows approval.";
        WatcherState = DashboardStateKind.Warning;
        try
        {
            while (!_watchCts.IsCancellationRequested)
            {
                await EvaluateWatcherAsync(_watchCts.Token).ConfigureAwait(true);
                await Task
                    .Delay(TimeSpan.FromSeconds(15), _timeProvider, _watchCts.Token)
                    .ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (
            _watchCts.IsCancellationRequested || _lifetime.IsCancellationRequested)
        {
        }
        finally
        {
            IsWatching = false;
            if (!IsSuspended)
            {
                WatcherStatus = "IDLE";
                WatcherDetail =
                    "Watching stopped. Rules were not deleted and hardware was not reset.";
                WatcherState = DashboardStateKind.Pending;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopWatching))]
    private void StopWatching()
    {
        _watchCts?.Cancel();
        IsWatching = false;
    }

    [RelayCommand(CanExecute = nameof(CanResumeWatcher))]
    private void ResumeWatcher()
    {
        _runService.Resume();
        IsSuspended = false;
        WatcherStatus = "IDLE";
        WatcherDetail = "The watcher is ready. Start watching to apply the next winning profile.";
        WatcherState = DashboardStateKind.Pending;
    }

    [RelayCommand(CanExecute = nameof(CanRefreshPowerSource))]
    private async Task RefreshPowerSourceAsync()
    {
        IsBusy = true;
        try
        {
            await CapturePowerSourceAsync(_lifetime.Token).ConfigureAwait(true);
            PreviewCurrentDraft();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSaveRule() => !IsBusy && HasValidDraftShape();

    private bool CanDeleteRule() => !IsBusy && SelectedRule is not null;

    private bool CanPreviewRule() => !IsBusy && HasValidDraftShape();

    private bool CanRefreshPowerSource() => !IsBusy && _initialized;

    private bool CanStartWatching() =>
        !IsBusy &&
        _initialized &&
        !IsWatching &&
        !IsSuspended &&
        _applyAsync is not null;

    private bool CanStopWatching() => IsWatching;

    private bool CanResumeWatcher() => !IsWatching && IsSuspended;

    public async Task EvaluateWatcherAsync(CancellationToken cancellationToken = default)
    {
        if (_applyAsync is null || _session is null)
            return;

        await CapturePowerSourceAsync(cancellationToken).ConfigureAwait(true);
        AutomationPreview selection = _previewService.Create(
            Rules.ToArray(),
            Profiles.ToArray(),
            _powerSourceSnapshot);
        ApplyPreview(selection);
        if (selection.Status != AutomationPreviewStatus.WouldSelect ||
            selection.SelectedProfile is null ||
            !selection.PowerSource.HasValue)
        {
            if (IsWatching && !IsSuspended)
            {
                WatcherStatus = "WATCHING";
                WatcherDetail = selection.ReasonCode is { } reason
                    ? $"No apply this tick · {reason}"
                    : "No apply this tick · the current power source has no winning rule.";
                WatcherState = DashboardStateKind.Warning;
            }

            return;
        }

        IReadOnlyList<HardwareWritePlanItem> operations;
        try
        {
            operations = ProfileApplyPlanner.Plan(_profilePreviewService.Create(
                selection.SelectedProfile,
                _session.HardwareStateSnapshot,
                _session.MachineSnapshot?.Capabilities ?? []));
        }
        catch (HardwareWriteException)
        {
            _runService.NoteBlocked();
            WatcherStatus = IsWatching ? "WATCHING" : "IDLE";
            WatcherDetail =
                "The winning profile is blocked or stale. Refresh hardware state, then wait for cooldown.";
            WatcherState = DashboardStateKind.Warning;
            return;
        }

        AutomationRunVerdict verdict = _runService.Evaluate(
            selection.SelectedProfile.Id,
            selection.PowerSource.Value,
            operations.Count > 0);
        switch (verdict)
        {
            case AutomationRunVerdict.SkipUnchanged:
                WatcherStatus = IsWatching ? "WATCHING" : "IDLE";
                WatcherDetail =
                    $"“{selection.SelectedProfile.Name}” already matches for {FormatPowerSource(selection.PowerSource.Value)}.";
                WatcherState = DashboardStateKind.Success;
                return;
            case AutomationRunVerdict.SkipCooldown:
                WatcherStatus = "COOLDOWN";
                WatcherDetail = _runService.CooldownUntilUtc is { } until
                    ? $"Next apply after {until.ToLocalTime():HH:mm:ss}."
                    : "Cooldown is active.";
                WatcherState = DashboardStateKind.Warning;
                return;
            case AutomationRunVerdict.Suspended:
                IsSuspended = true;
                WatcherStatus = "SUSPENDED";
                WatcherDetail = _runService.SuspendReason ?? "automation_suspended";
                WatcherState = DashboardStateKind.Error;
                _watchCts?.Cancel();
                return;
            case AutomationRunVerdict.Apply:
                break;
            default:
                return;
        }

        if (operations.Count == 0)
            return;

        try
        {
            HardwareStateSnapshot snapshot = await _applyAsync(operations, cancellationToken)
                .ConfigureAwait(true);
            _session.UpdateHardwareStateSnapshot(snapshot);
            _runService.NoteSuccess(
                selection.SelectedProfile.Id,
                selection.PowerSource.Value);
            WatcherStatus = "APPLIED";
            WatcherDetail =
                $"Applied “{selection.SelectedProfile.Name}” for {FormatPowerSource(selection.PowerSource.Value)}.";
            WatcherState = DashboardStateKind.Success;
            WorkspaceTitle = "Automation applied a profile";
            WorkspaceMessage = WatcherDetail;
            WorkspaceState = DashboardStateKind.Success;
        }
        catch (HardwareWriteException exception)
        {
            ApplyWatcherFailure(exception.ErrorCode);
        }
        catch (DashboardDataSourceException exception)
        {
            ApplyWatcherFailure(exception.ErrorCode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            ApplyWatcherFailure("broker_write_failed");
        }
    }

    private void ApplyWatcherFailure(string errorCode)
    {
        if (errorCode == "broker_elevation_cancelled")
        {
            _runService.NoteCancel();
            WatcherStatus = "COOLDOWN";
            WatcherDetail = "Windows approval was cancelled. The watcher will wait before asking again.";
            WatcherState = DashboardStateKind.Warning;
            return;
        }

        _runService.NoteFailure(errorCode);
        if (_runService.IsSuspended)
        {
            IsSuspended = true;
            WatcherStatus = "SUSPENDED";
            WatcherDetail = $"Readback failed · {errorCode}. Resume after you trust hardware state.";
            WatcherState = DashboardStateKind.Error;
            WorkspaceTitle = "Automation suspended";
            WorkspaceMessage = WatcherDetail;
            WorkspaceState = DashboardStateKind.Error;
            _watchCts?.Cancel();
            return;
        }

        WatcherStatus = "COOLDOWN";
        WatcherDetail = $"{errorCode} · the watcher will wait before trying again.";
        WatcherState = DashboardStateKind.Warning;
    }

    private bool HasValidDraftShape()
    {
        string name = DraftName?.Trim() ?? string.Empty;
        return name.Length is > 0 and <= AutomationRule.MaximumNameLength
            && !name.Any(char.IsControl)
            && SelectedProfile is not null
            && Enum.IsDefined(SelectedPowerSource)
            && TryParsePriority(out _);
    }

    private bool TryParsePriority(out int priority) =>
        int.TryParse(
            DraftPriority?.Trim(),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out priority)
        && priority is >= AutomationRule.MinimumPriority
            and <= AutomationRule.MaximumPriority;

    private AutomationRule BuildDraft()
    {
        if (!TryParsePriority(out int priority))
        {
            throw new ArgumentOutOfRangeException(
                nameof(DraftPriority),
                $"Priority must be {AutomationRule.MinimumPriority}–{AutomationRule.MaximumPriority}.");
        }

        return new AutomationRule(
            _draftId,
            DraftName,
            SelectedProfile?.Id
                ?? throw new ArgumentException("Select a target profile.", nameof(SelectedProfile)),
            SelectedPowerSource,
            priority,
            DraftIsEnabled);
    }

    private void HydrateDraft(AutomationRule? rule)
    {
        _isHydratingDraft = true;
        try
        {
            SelectedRule = rule;
            _draftId = rule?.Id ?? AutomationRuleId.New();
            DraftName = rule?.Name ?? "New rule";
            SelectedProfile = rule is null
                ? Profiles.FirstOrDefault()
                : Profiles.FirstOrDefault(profile => profile.Id == rule.ProfileId);
            SelectedPowerSource = rule?.RequiredPowerSource ?? PowerSourceKind.Ac;
            DraftPriority = (rule?.Priority ?? 100).ToString(CultureInfo.InvariantCulture);
            DraftIsEnabled = rule?.IsEnabled ?? true;
        }
        finally
        {
            _isHydratingDraft = false;
        }
    }

    private async Task CapturePowerSourceAsync(CancellationToken cancellationToken)
    {
        try
        {
            _powerSourceSnapshot = await _powerSourceService
                .CaptureAsync(cancellationToken)
                .ConfigureAwait(true);
            ApplyPowerSource(_powerSourceSnapshot);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            _powerSourceSnapshot = null;
            PowerSourceValue = "Unavailable";
            PowerSourceDetail = "The local Windows power-status read failed.";
            PowerSourceState = DashboardStateKind.Error;
        }
    }

    private void ApplyPowerSource(PowerSourceSnapshot snapshot)
    {
        if (snapshot.PowerSource.Status == HardwareReadStatus.Success
            && snapshot.PowerSource.Value.HasValue)
        {
            PowerSourceValue = FormatPowerSource(snapshot.PowerSource.Value.Value);
            PowerSourceDetail =
                $"Observed {snapshot.ObservedAt.ToLocalTime():HH:mm:ss} · local Windows API · no elevation";
            PowerSourceState = DashboardStateKind.Success;
            return;
        }

        PowerSourceValue = "Unavailable";
        PowerSourceDetail = snapshot.PowerSource.ErrorCode ?? "power_source_unavailable";
        PowerSourceState = snapshot.PowerSource.Status == HardwareReadStatus.InvalidData
            ? DashboardStateKind.Error
            : DashboardStateKind.Unavailable;
    }

    private void PreviewCurrentDraft()
    {
        if (HasValidDraftShape())
        {
            ApplyPreview(CreateDraftPreview(BuildDraft()));
            return;
        }

        if (SelectedRule is not null)
        {
            ApplyPreview(_previewService.Create(
                Rules.ToArray(),
                Profiles.ToArray(),
                _powerSourceSnapshot));
            return;
        }

        ApplyInvalidDraft();
    }

    private AutomationPreview CreateDraftPreview(AutomationRule draft)
    {
        AutomationRule[] candidates =
        [
            .. Rules.Where(rule => rule.Id != draft.Id),
            draft,
        ];
        return _previewService.Create(
            candidates,
            Profiles.ToArray(),
            _powerSourceSnapshot);
    }

    private void ApplyPreview(AutomationPreview preview)
    {
        LastPreview = preview;
        PreviewRuleName = preview.SelectedRule?.Name ?? "—";
        PreviewProfileName = preview.SelectedProfile?.Name ?? "—";

        switch (preview.Status)
        {
            case AutomationPreviewStatus.WouldSelect:
                PreviewStatus = "WOULD SELECT";
                PreviewDetail =
                    $"Priority {preview.SelectedRule!.Priority} wins for {FormatPowerSource(preview.PowerSource!.Value)}.";
                WorkspaceTitle = "Deterministic selection previewed";
                WorkspaceMessage =
                    $"“{preview.SelectedRule.Name}” would select “{preview.SelectedProfile!.Name}”. Start watching to apply it with Windows approval.";
                WorkspaceState = DashboardStateKind.Warning;
                PreviewState = DashboardStateKind.Warning;
                break;

            case AutomationPreviewStatus.NoEnabledRules:
                ApplyBlockedPreview(
                    "NO ENABLED RULE",
                    "No enabled rule participates in selection.",
                    "No enabled automation",
                    DashboardStateKind.Unavailable);
                break;

            case AutomationPreviewStatus.PowerSourceUnavailable:
                ApplyBlockedPreview(
                    "SOURCE UNAVAILABLE",
                    "A current AC/battery observation is required.",
                    "Power source unavailable",
                    DashboardStateKind.Unavailable);
                break;

            case AutomationPreviewStatus.PowerSourceStale:
                ApplyBlockedPreview(
                    "REFRESH REQUIRED",
                    "The AC/battery observation is too old to trust.",
                    "Power source refresh required",
                    DashboardStateKind.Warning);
                break;

            case AutomationPreviewStatus.NoMatch:
                ApplyBlockedPreview(
                    "NO MATCH",
                    "No enabled rule matches the observed power source.",
                    "No matching rule",
                    DashboardStateKind.Warning);
                break;

            case AutomationPreviewStatus.Ambiguous:
                ApplyBlockedPreview(
                    "AMBIGUOUS",
                    "Multiple matching rules share the winning priority.",
                    "Rule priority conflict",
                    DashboardStateKind.Error);
                break;

            case AutomationPreviewStatus.ProfileMissing:
                ApplyBlockedPreview(
                    "PROFILE MISSING",
                    "The winning rule references a missing or duplicate profile.",
                    "Target profile unavailable",
                    DashboardStateKind.Error);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported automation preview status: {preview.Status}.");
        }
    }

    private void ApplyBlockedPreview(
        string status,
        string detail,
        string title,
        DashboardStateKind state)
    {
        PreviewStatus = status;
        PreviewDetail = $"{detail} · {LastPreview!.ReasonCode}";
        WorkspaceTitle = title;
        WorkspaceMessage = $"{detail} No profile or hardware action is available.";
        WorkspaceState = state;
        PreviewState = state;
    }

    private void MarkDraftChanged()
    {
        if (_isHydratingDraft)
            return;

        LastPreview = null;
        PreviewStatus = "PREVIEW REQUIRED";
        PreviewRuleName = "—";
        PreviewProfileName = "—";
        PreviewDetail = "The local draft changed. Preview it against the latest power-source observation.";
        PreviewState = DashboardStateKind.Pending;
        WorkspaceTitle = "Rule draft changed";
        WorkspaceMessage =
            "Select Preview rule to evaluate deterministic selection. Nothing runs automatically.";
        WorkspaceState = DashboardStateKind.Pending;
    }

    private void ApplyInvalidDraft()
    {
        LastPreview = null;
        PreviewStatus = "DRAFT INCOMPLETE";
        PreviewRuleName = "—";
        PreviewProfileName = "—";
        PreviewDetail =
            $"Use a valid name, profile, power source, and priority {AutomationRule.MinimumPriority}–{AutomationRule.MaximumPriority}.";
        PreviewState = DashboardStateKind.Warning;
        WorkspaceTitle = Profiles.Count == 0
            ? "Create a profile draft first"
            : "Rule draft needs attention";
        WorkspaceMessage = Profiles.Count == 0
            ? "Automation rules can only reference a saved local profile. No action is available."
            : PreviewDetail;
        WorkspaceState = DashboardStateKind.Warning;
    }

    private void ApplyStoreFailure(string errorCode, bool isRuleStore)
    {
        LastPreview = null;
        PreviewStatus = "STORE UNAVAILABLE";
        PreviewRuleName = "—";
        PreviewProfileName = "—";
        PreviewDetail = errorCode;
        PreviewState = DashboardStateKind.Error;
        WorkspaceTitle = isRuleStore
            ? "Local automation storage unavailable"
            : "Local profile storage unavailable";
        WorkspaceMessage = errorCode switch
        {
            "automation_rule_store_invalid_json"
                or "automation_rule_store_invalid_document"
                or "profile_store_invalid_json"
                or "profile_store_invalid_document" =>
                "The local JSON file is invalid and was not overwritten.",
            "automation_rule_store_schema_unsupported"
                or "profile_store_schema_unsupported" =>
                "The local JSON file uses a schema this build does not understand.",
            "automation_rule_store_access_denied"
                or "profile_store_access_denied" =>
                "Windows denied access to the local draft file.",
            "automation_rule_store_busy"
                or "profile_store_busy" =>
                "Another app instance is using the local draft file. Try again in a moment.",
            _ => "The local storage operation failed without changing hardware.",
        };
        WorkspaceState = errorCode is "automation_rule_store_busy" or "profile_store_busy"
            ? DashboardStateKind.Warning
            : DashboardStateKind.Error;
    }

    private void ApplyUnexpectedFailure()
    {
        LastPreview = null;
        PreviewStatus = "WORKSPACE UNAVAILABLE";
        PreviewRuleName = "—";
        PreviewProfileName = "—";
        PreviewDetail = "The local automation operation failed.";
        PreviewState = DashboardStateKind.Error;
        WorkspaceTitle = "Automation preview unavailable";
        WorkspaceMessage = "The failure did not apply a profile or change hardware.";
        WorkspaceState = DashboardStateKind.Error;
    }

    private void ReplaceRules(IEnumerable<AutomationRule> rules)
    {
        Rules.Clear();
        foreach (AutomationRule rule in rules)
            Rules.Add(rule);
    }

    private void ReplaceProfiles(IEnumerable<HardwareProfile> profiles)
    {
        Profiles.Clear();
        foreach (HardwareProfile profile in profiles)
            Profiles.Add(profile);
    }

    private void UpsertRule(AutomationRule rule)
    {
        AutomationRule? existing = Rules.FirstOrDefault(item => item.Id == rule.Id);
        if (existing is not null)
            Rules.Remove(existing);

        int index = 0;
        while (index < Rules.Count && CompareRules(Rules[index], rule) <= 0)
            index++;

        Rules.Insert(index, rule);
    }

    private static int CompareRules(AutomationRule left, AutomationRule right)
    {
        int byPriority = right.Priority.CompareTo(left.Priority);
        if (byPriority != 0)
            return byPriority;

        int byName = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        return byName != 0 ? byName : left.Id.Value.CompareTo(right.Id.Value);
    }

    private static string FormatPowerSource(PowerSourceKind powerSource) =>
        powerSource == PowerSourceKind.Ac ? "AC power" : "Battery power";
}
