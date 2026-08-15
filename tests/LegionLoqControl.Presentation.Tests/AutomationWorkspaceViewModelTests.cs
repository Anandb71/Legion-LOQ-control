using LegionLoqControl.Application.Automation;
using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Application.Profiles;
using LegionLoqControl.Domain.Automation;
using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Profiles;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Services;
using LegionLoqControl.ViewModels;
using Xunit;

namespace LegionLoqControl.Presentation.Tests;

public sealed class AutomationWorkspaceViewModelTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Initialization_previews_the_deterministic_saved_selection()
    {
        HardwareProfile profile = CreateProfile("AC performance");
        AutomationRule rule = CreateRule(
            "Plugged in",
            profile.Id,
            PowerSourceKind.Ac,
            priority: 200);
        var ruleStore = new StubRuleStore(rule);
        var powerReader = new StubPowerSourceReader(PowerSourceKind.Ac);
        using var viewModel = CreateViewModel(
            ruleStore,
            new StubProfileStore(profile),
            powerReader);

        await viewModel.InitializeAsync();

        Assert.Equal(rule, viewModel.SelectedRule);
        Assert.Equal(profile, viewModel.SelectedProfile);
        Assert.Equal(AutomationPreviewStatus.WouldSelect, viewModel.LastPreview?.Status);
        Assert.Equal("WOULD SELECT", viewModel.PreviewStatus);
        Assert.Equal("Plugged in", viewModel.PreviewRuleName);
        Assert.Equal("AC performance", viewModel.PreviewProfileName);
        Assert.Equal("AC power", viewModel.PowerSourceValue);
        Assert.Equal(1, powerReader.ReadCount);
    }

    [Fact]
    public async Task Saving_a_rule_only_calls_the_local_rule_store()
    {
        HardwareProfile profile = CreateProfile("Travel");
        var ruleStore = new StubRuleStore();
        var powerReader = new StubPowerSourceReader(PowerSourceKind.Battery);
        using var viewModel = CreateViewModel(
            ruleStore,
            new StubProfileStore(profile),
            powerReader);
        await viewModel.InitializeAsync();
        viewModel.DraftName = "On battery";
        viewModel.SelectedProfile = profile;
        viewModel.SelectedPowerSource = PowerSourceKind.Battery;
        viewModel.DraftPriority = "350";
        viewModel.DraftIsEnabled = true;

        await viewModel.SaveRuleCommand.ExecuteAsync(null);

        AutomationRule saved = Assert.Single(ruleStore.Rules);
        Assert.Equal("On battery", saved.Name);
        Assert.Equal(profile.Id, saved.ProfileId);
        Assert.Equal(PowerSourceKind.Battery, saved.RequiredPowerSource);
        Assert.Equal(350, saved.Priority);
        Assert.True(saved.IsEnabled);
        Assert.Equal(1, ruleStore.SaveCount);
        Assert.Equal(1, powerReader.ReadCount);
    }

    [Fact]
    public async Task Highest_unique_priority_wins_regardless_of_list_order()
    {
        HardwareProfile quiet = CreateProfile("Quiet");
        HardwareProfile performance = CreateProfile("Performance");
        AutomationRule low = CreateRule(
            "Low",
            quiet.Id,
            PowerSourceKind.Ac,
            priority: 20);
        AutomationRule high = CreateRule(
            "High",
            performance.Id,
            PowerSourceKind.Ac,
            priority: 900);
        using var viewModel = CreateViewModel(
            new StubRuleStore(low, high),
            new StubProfileStore(quiet, performance),
            new StubPowerSourceReader(PowerSourceKind.Ac));

        await viewModel.InitializeAsync();

        Assert.Equal(AutomationPreviewStatus.WouldSelect, viewModel.LastPreview?.Status);
        Assert.Equal("High", viewModel.PreviewRuleName);
        Assert.Equal("Performance", viewModel.PreviewProfileName);
    }

    [Fact]
    public async Task Equal_winning_priorities_are_exposed_as_a_conflict()
    {
        HardwareProfile first = CreateProfile("First");
        HardwareProfile second = CreateProfile("Second");
        AutomationRule firstRule = CreateRule(
            "First rule",
            first.Id,
            PowerSourceKind.Ac,
            priority: 500);
        AutomationRule secondRule = CreateRule(
            "Second rule",
            second.Id,
            PowerSourceKind.Ac,
            priority: 500);
        using var viewModel = CreateViewModel(
            new StubRuleStore(firstRule, secondRule),
            new StubProfileStore(first, second),
            new StubPowerSourceReader(PowerSourceKind.Ac));

        await viewModel.InitializeAsync();

        Assert.Equal(AutomationPreviewStatus.Ambiguous, viewModel.LastPreview?.Status);
        Assert.Equal("AMBIGUOUS", viewModel.PreviewStatus);
        Assert.Equal(DashboardStateKind.Error, viewModel.PreviewState);
        Assert.Contains(
            "automation_priority_ambiguous",
            viewModel.PreviewDetail,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refreshing_power_source_recalculates_without_elevation()
    {
        HardwareProfile profile = CreateProfile("AC profile");
        AutomationRule rule = CreateRule(
            "AC rule",
            profile.Id,
            PowerSourceKind.Ac,
            priority: 100);
        var powerReader = new StubPowerSourceReader(
            PowerSourceKind.Battery,
            PowerSourceKind.Ac);
        using var viewModel = CreateViewModel(
            new StubRuleStore(rule),
            new StubProfileStore(profile),
            powerReader);
        await viewModel.InitializeAsync();

        Assert.Equal(AutomationPreviewStatus.NoMatch, viewModel.LastPreview?.Status);

        await viewModel.RefreshPowerSourceCommand.ExecuteAsync(null);

        Assert.Equal(AutomationPreviewStatus.WouldSelect, viewModel.LastPreview?.Status);
        Assert.Equal("AC power", viewModel.PowerSourceValue);
        Assert.Equal(2, powerReader.ReadCount);
    }

    [Fact]
    public async Task Missing_target_profile_is_reported_without_mutation()
    {
        AutomationRule rule = CreateRule(
            "Orphaned",
            ProfileId.New(),
            PowerSourceKind.Ac,
            priority: 100);
        var ruleStore = new StubRuleStore(rule);
        using var viewModel = CreateViewModel(
            ruleStore,
            new StubProfileStore(),
            new StubPowerSourceReader(PowerSourceKind.Ac));

        await viewModel.InitializeAsync();

        Assert.Equal(AutomationPreviewStatus.ProfileMissing, viewModel.LastPreview?.Status);
        Assert.Equal("PROFILE MISSING", viewModel.PreviewStatus);
        Assert.Equal(0, ruleStore.SaveCount);
        Assert.Equal(0, ruleStore.DeleteCount);
    }

    [Fact]
    public async Task Profile_catalog_changes_recalculate_saved_rule_targets()
    {
        ProfileId profileId = ProfileId.New();
        AutomationRule rule = CreateRule(
            "Waiting for profile",
            profileId,
            PowerSourceKind.Ac,
            priority: 100);
        using var viewModel = CreateViewModel(
            new StubRuleStore(rule),
            new StubProfileStore(),
            new StubPowerSourceReader(PowerSourceKind.Ac));
        await viewModel.InitializeAsync();
        Assert.Equal(AutomationPreviewStatus.ProfileMissing, viewModel.LastPreview?.Status);
        var profile = new HardwareProfile(
            profileId,
            "Arrived",
            new HardwareProfileTargets(thermalMode: ThermalMode.Balanced));

        viewModel.SynchronizeProfiles([profile]);

        Assert.Equal(profile, viewModel.SelectedProfile);
        Assert.Equal(AutomationPreviewStatus.WouldSelect, viewModel.LastPreview?.Status);
        Assert.Equal("Arrived", viewModel.PreviewProfileName);
    }

    [Fact]
    public async Task Corrupt_rule_store_is_reported_without_overwriting_it()
    {
        var ruleStore = new StubRuleStore
        {
            LoadException = new AutomationRuleStoreException(
                "automation_rule_store_invalid_document"),
        };
        using var viewModel = CreateViewModel(
            ruleStore,
            new StubProfileStore(CreateProfile("Unused")),
            new StubPowerSourceReader(PowerSourceKind.Ac));

        await viewModel.InitializeAsync();

        Assert.Equal("Local automation storage unavailable", viewModel.WorkspaceTitle);
        Assert.Equal(DashboardStateKind.Error, viewModel.WorkspaceState);
        Assert.Equal(0, ruleStore.SaveCount);
        Assert.Equal(0, ruleStore.DeleteCount);
    }

    [Fact]
    public async Task Busy_rule_store_is_reported_as_retryable()
    {
        var ruleStore = new StubRuleStore
        {
            LoadException = new AutomationRuleStoreException(
                "automation_rule_store_busy"),
        };
        using var viewModel = CreateViewModel(
            ruleStore,
            new StubProfileStore(CreateProfile("Unused")),
            new StubPowerSourceReader(PowerSourceKind.Ac));

        await viewModel.InitializeAsync();

        Assert.Equal("Local automation storage unavailable", viewModel.WorkspaceTitle);
        Assert.Equal(DashboardStateKind.Warning, viewModel.WorkspaceState);
        Assert.Contains("Another app instance", viewModel.WorkspaceMessage);
        Assert.Equal(0, ruleStore.SaveCount);
        Assert.Equal(0, ruleStore.DeleteCount);
    }

    [Fact]
    public async Task Delete_removes_only_the_selected_local_rule()
    {
        HardwareProfile profile = CreateProfile("Shared");
        AutomationRule first = CreateRule(
            "First",
            profile.Id,
            PowerSourceKind.Ac,
            priority: 20);
        AutomationRule second = CreateRule(
            "Second",
            profile.Id,
            PowerSourceKind.Battery,
            priority: 10);
        var ruleStore = new StubRuleStore(first, second);
        using var viewModel = CreateViewModel(
            ruleStore,
            new StubProfileStore(profile),
            new StubPowerSourceReader(PowerSourceKind.Ac));
        await viewModel.InitializeAsync();
        viewModel.SelectedRule = second;

        await viewModel.DeleteRuleCommand.ExecuteAsync(null);

        Assert.Equal([first], ruleStore.Rules);
        Assert.Equal([first], viewModel.Rules);
        Assert.Equal(1, ruleStore.DeleteCount);
        Assert.Equal("Rule deleted", viewModel.WorkspaceTitle);
    }

    [Fact]
    public async Task Watching_stays_disabled_without_an_apply_callback()
    {
        using var viewModel = CreateViewModel(
            new StubRuleStore(),
            new StubProfileStore(CreateProfile("Unused")),
            new StubPowerSourceReader(PowerSourceKind.Ac));
        await viewModel.InitializeAsync();

        Assert.False(viewModel.StartWatchingCommand.CanExecute(null));
        Assert.False(viewModel.ResumeWatcherCommand.CanExecute(null));
    }

    [Fact]
    public async Task Watcher_tick_applies_the_winning_profile_once()
    {
        HardwareProfile profile = CreateProfile("Quiet AC", ThermalMode.Quiet);
        AutomationRule rule = CreateRule(
            "Plugged in",
            profile.Id,
            PowerSourceKind.Ac,
            priority: 200);
        var session = new MachineSessionViewModel();
        session.UpdateMachineSnapshot(CreateMachineSnapshot());
        session.UpdateHardwareStateSnapshot(CreateHardwareSnapshot(ThermalMode.Balanced));
        List<HardwareWritePlanItem> applied = [];
        using var viewModel = CreateViewModel(
            new StubRuleStore(rule),
            new StubProfileStore(profile),
            new StubPowerSourceReader(PowerSourceKind.Ac),
            session,
            (operations, _) =>
            {
                applied.AddRange(operations);
                return ValueTask.FromResult(CreateHardwareSnapshot(ThermalMode.Quiet));
            });
        await viewModel.InitializeAsync();

        await viewModel.EvaluateWatcherAsync(TestContext.Current.CancellationToken);

        HardwareWritePlanItem operation = Assert.Single(applied);
        Assert.Equal(HardwareWriteKind.ThermalMode, operation.Kind);
        Assert.Equal(nameof(ThermalMode.Balanced), operation.Expected);
        Assert.Equal(nameof(ThermalMode.Quiet), operation.Desired);
        Assert.Equal("APPLIED", viewModel.WatcherStatus);
        Assert.False(viewModel.IsSuspended);
    }

    [Fact]
    public async Task Watcher_suspends_after_a_failed_readback()
    {
        HardwareProfile profile = CreateProfile("Quiet AC", ThermalMode.Quiet);
        AutomationRule rule = CreateRule(
            "Plugged in",
            profile.Id,
            PowerSourceKind.Ac,
            priority: 200);
        var session = new MachineSessionViewModel();
        session.UpdateMachineSnapshot(CreateMachineSnapshot());
        session.UpdateHardwareStateSnapshot(CreateHardwareSnapshot(ThermalMode.Balanced));
        using var viewModel = CreateViewModel(
            new StubRuleStore(rule),
            new StubProfileStore(profile),
            new StubPowerSourceReader(PowerSourceKind.Ac),
            session,
            (_, _) => throw new DashboardDataSourceException("thermal_readback_mismatch"));
        await viewModel.InitializeAsync();

        await viewModel.EvaluateWatcherAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsSuspended);
        Assert.Equal("SUSPENDED", viewModel.WatcherStatus);
        Assert.True(viewModel.ResumeWatcherCommand.CanExecute(null));
    }

    private static AutomationWorkspaceViewModel CreateViewModel(
        IAutomationRuleStore ruleStore,
        IProfileStore profileStore,
        IPowerSourceReader powerSourceReader,
        MachineSessionViewModel? session = null,
        Func<IReadOnlyList<HardwareWritePlanItem>, CancellationToken, ValueTask<HardwareStateSnapshot>>?
            applyAsync = null)
    {
        var timeProvider = new FixedTimeProvider(Now);
        return new AutomationWorkspaceViewModel(
            ruleStore,
            profileStore,
            new PowerSourceService(powerSourceReader, timeProvider),
            new AutomationPreviewService(timeProvider),
            session,
            new ProfilePreviewService(timeProvider),
            new AutomationRunService(timeProvider, TimeSpan.FromMinutes(2)),
            applyAsync,
            timeProvider);
    }

    private static HardwareProfile CreateProfile(
        string name,
        ThermalMode thermalMode = ThermalMode.Balanced) =>
        new(
            ProfileId.New(),
            name,
            new HardwareProfileTargets(thermalMode: thermalMode));

    private static MachineSnapshot CreateMachineSnapshot()
    {
        var identity = new MachineIdentity(
            Observation.FromValue("LENOVO"),
            Observation.FromValue("LOQ"),
            Observation.FromValue("LOQ 15IRX9"),
            Observation.FromValue("83DV"),
            Observation.FromValue("NECN50WW"));
        return new MachineSnapshot(
            identity,
            Now,
            [
                new CapabilityEvidence(
                    HardwareCapability.ThermalMode,
                    CapabilitySupport.Unknown,
                    "test",
                    Now,
                    "wmi_interface_present_unverified"),
            ]);
    }

    private static HardwareStateSnapshot CreateHardwareSnapshot(ThermalMode thermalMode) =>
        new(
            Now,
            HardwareReadResult<BatteryChargeMode>.Success(BatteryChargeMode.Normal),
            HardwareReadResult<ThermalMode>.Success(thermalMode),
            HardwareReadResult<ToggleState>.Success(ToggleState.Disabled),
            HardwareReadResult<IntegratedGpuMode>.Success(IntegratedGpuMode.Default),
            HardwareReadResult<FourZoneKeyboardMode>.Success(FourZoneKeyboardMode.Unknown),
            HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.Unavailable,
                "fan_table_not_opened"));

    private static AutomationRule CreateRule(
        string name,
        ProfileId profileId,
        PowerSourceKind powerSource,
        int priority) =>
        new(
            AutomationRuleId.New(),
            name,
            profileId,
            powerSource,
            priority,
            isEnabled: true);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubPowerSourceReader(
        params PowerSourceKind[] powerSources) : IPowerSourceReader
    {
        private readonly Queue<PowerSourceKind> _powerSources = new(powerSources);
        private PowerSourceKind? _last;

        public int ReadCount { get; private set; }

        public ValueTask<HardwareReadResult<PowerSourceKind>> ReadAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;

            if (_powerSources.Count > 0)
                _last = _powerSources.Dequeue();

            return ValueTask.FromResult(
                _last.HasValue
                    ? HardwareReadResult<PowerSourceKind>.Success(_last.Value)
                    : HardwareReadResult<PowerSourceKind>.Failure(
                        HardwareReadStatus.Unavailable,
                        "test_power_source_unavailable"));
        }
    }

    private sealed class StubRuleStore(params AutomationRule[] rules) : IAutomationRuleStore
    {
        public List<AutomationRule> Rules { get; } = [.. rules];

        public int SaveCount { get; private set; }

        public int DeleteCount { get; private set; }

        public AutomationRuleStoreException? LoadException { get; init; }

        public ValueTask<IReadOnlyList<AutomationRule>> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (LoadException is not null)
            {
                return ValueTask.FromException<IReadOnlyList<AutomationRule>>(
                    LoadException);
            }

            return ValueTask.FromResult<IReadOnlyList<AutomationRule>>(
                Array.AsReadOnly(Rules.ToArray()));
        }

        public ValueTask SaveAsync(
            AutomationRule rule,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCount++;
            Rules.RemoveAll(item => item.Id == rule.Id);
            Rules.Add(rule);
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> DeleteAsync(
            AutomationRuleId id,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeleteCount++;
            return ValueTask.FromResult(Rules.RemoveAll(item => item.Id == id) > 0);
        }
    }

    private sealed class StubProfileStore(params HardwareProfile[] profiles) : IProfileStore
    {
        private readonly IReadOnlyList<HardwareProfile> _profiles =
            Array.AsReadOnly(profiles);

        public ValueTask<IReadOnlyList<HardwareProfile>> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_profiles);
        }

        public ValueTask SaveAsync(
            HardwareProfile profile,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<bool> DeleteAsync(
            ProfileId id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
