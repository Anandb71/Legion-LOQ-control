using LegionLoqControl.Application.Profiles;
using LegionLoqControl.Domain.Capabilities;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Profiles;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.ViewModels;
using Xunit;

namespace LegionLoqControl.Presentation.Tests;

public sealed class ProfileWorkspaceViewModelTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Empty_store_starts_with_a_local_unavailable_preview()
    {
        var store = new StubProfileStore();
        var session = new MachineSessionViewModel();
        using var viewModel = CreateViewModel(store, session);

        await viewModel.InitializeAsync();

        Assert.Empty(viewModel.Profiles);
        Assert.Null(viewModel.SelectedProfile);
        Assert.Equal("Create a local draft", viewModel.WorkspaceTitle);
        Assert.All(
            viewModel.PreviewItems,
            item => Assert.Equal("UNAVAILABLE", item.Status));
    }

    [Fact]
    public async Task Saving_a_draft_only_calls_the_local_profile_store()
    {
        var store = new StubProfileStore();
        var session = new MachineSessionViewModel();
        using var viewModel = CreateViewModel(store, session);
        await viewModel.InitializeAsync();
        viewModel.DraftName = "Travel";
        viewModel.IncludeBattery = true;
        viewModel.SelectedBatteryMode = BatteryChargeMode.Conservation;
        viewModel.IncludeThermal = false;

        await viewModel.SaveDraftCommand.ExecuteAsync(null);

        HardwareProfile saved = Assert.Single(store.Profiles);
        Assert.Equal("Travel", saved.Name);
        Assert.Equal(BatteryChargeMode.Conservation, saved.Targets.BatteryChargeMode);
        Assert.Null(saved.Targets.ThermalMode);
        Assert.Same(saved, viewModel.SelectedProfile);
        Assert.Equal("Draft saved locally", viewModel.WorkspaceTitle);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Typed_session_changes_recalculate_the_preview()
    {
        HardwareProfile profile = CreateProfile("Quiet", ThermalMode.Quiet);
        var store = new StubProfileStore(profile);
        var session = new MachineSessionViewModel();
        session.UpdateMachineSnapshot(CreateMachineSnapshot(
            CapabilitySupport.Supported));
        session.UpdateHardwareStateSnapshot(CreateHardwareSnapshot(
            ThermalMode.Balanced));
        using var viewModel = CreateViewModel(store, session);
        await viewModel.InitializeAsync();

        Assert.Equal(
            ProfileTargetPreviewState.WouldChange,
            viewModel.LastPreview?.ThermalMode?.State);

        session.UpdateHardwareStateSnapshot(CreateHardwareSnapshot(ThermalMode.Quiet));

        Assert.Equal(
            ProfileTargetPreviewState.Matches,
            viewModel.LastPreview?.ThermalMode?.State);
        Assert.Equal("Draft already matches", viewModel.WorkspaceTitle);
    }

    [Fact]
    public async Task Corrupt_store_is_reported_without_overwriting_it()
    {
        var store = new StubProfileStore
        {
            LoadException = new ProfileStoreException(
                "profile_store_invalid_document"),
        };
        using var viewModel = CreateViewModel(
            store,
            new MachineSessionViewModel());

        await viewModel.InitializeAsync();

        Assert.Equal("Local profile storage unavailable", viewModel.WorkspaceTitle);
        Assert.Equal(DashboardStateKind.Error, viewModel.WorkspaceState);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, store.DeleteCount);
    }

    [Fact]
    public async Task Delete_removes_only_the_selected_local_draft()
    {
        HardwareProfile first = CreateProfile("First", ThermalMode.Quiet);
        HardwareProfile second = CreateProfile("Second", ThermalMode.Balanced);
        var store = new StubProfileStore(first, second);
        using var viewModel = CreateViewModel(
            store,
            new MachineSessionViewModel());
        await viewModel.InitializeAsync();
        viewModel.SelectedProfile = second;

        await viewModel.DeleteDraftCommand.ExecuteAsync(null);

        Assert.Equal([first], store.Profiles);
        Assert.Equal([first], viewModel.Profiles);
        Assert.Equal(1, store.DeleteCount);
        Assert.Equal("Draft deleted", viewModel.WorkspaceTitle);
    }

    [Fact]
    public void Workspace_exposes_no_apply_command()
    {
        string[] publicMembers = typeof(ProfileWorkspaceViewModel)
            .GetMembers()
            .Select(member => member.Name)
            .ToArray();

        Assert.DoesNotContain(publicMembers, name =>
            name.Contains("Apply", StringComparison.OrdinalIgnoreCase));
    }

    private static ProfileWorkspaceViewModel CreateViewModel(
        IProfileStore store,
        MachineSessionViewModel session) =>
        new(
            store,
            new ProfilePreviewService(new FixedTimeProvider(Now)),
            session);

    private static HardwareProfile CreateProfile(string name, ThermalMode thermalMode) =>
        new(
            ProfileId.New(),
            name,
            new HardwareProfileTargets(thermalMode: thermalMode));

    private static MachineSnapshot CreateMachineSnapshot(CapabilitySupport support)
    {
        var identity = new MachineIdentity(
            Observation.FromValue("LENOVO"),
            Observation.FromValue("LOQ"),
            Observation.FromValue("LOQ 15IRX9"),
            Observation.FromValue("83DV"),
            Observation.FromValue("NECN50WW"));
        CapabilityEvidence[] evidence =
        [
            new(
                HardwareCapability.ThermalMode,
                support,
                "test",
                Now,
                support == CapabilitySupport.Supported
                    ? "verified"
                    : "unverified"),
        ];
        return new MachineSnapshot(identity, Now, evidence);
    }

    private static HardwareStateSnapshot CreateHardwareSnapshot(ThermalMode thermalMode) =>
        new(
            Now,
            HardwareReadResult<BatteryChargeMode>.Success(BatteryChargeMode.Normal),
            HardwareReadResult<ThermalMode>.Success(thermalMode),
            HardwareReadResult<ToggleState>.Success(ToggleState.Disabled),
            HardwareReadResult<IntegratedGpuMode>.Success(IntegratedGpuMode.Default));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubProfileStore(params HardwareProfile[] profiles) : IProfileStore
    {
        public List<HardwareProfile> Profiles { get; } = [.. profiles];

        public int SaveCount { get; private set; }

        public int DeleteCount { get; private set; }

        public ProfileStoreException? LoadException { get; init; }

        public ValueTask<IReadOnlyList<HardwareProfile>> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (LoadException is not null)
                return ValueTask.FromException<IReadOnlyList<HardwareProfile>>(
                    LoadException);

            return ValueTask.FromResult<IReadOnlyList<HardwareProfile>>(
                Array.AsReadOnly(Profiles.ToArray()));
        }

        public ValueTask SaveAsync(
            HardwareProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCount++;
            Profiles.RemoveAll(item => item.Id == profile.Id);
            Profiles.Add(profile);
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> DeleteAsync(
            ProfileId id,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeleteCount++;
            return ValueTask.FromResult(
                Profiles.RemoveAll(item => item.Id == id) > 0);
        }
    }
}

