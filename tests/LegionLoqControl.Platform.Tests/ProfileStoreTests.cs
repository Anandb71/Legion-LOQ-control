using LegionLoqControl.Application.Profiles;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Profiles;
using LegionLoqControl.Infrastructure.Windows.Profiles;
using Xunit;

namespace LegionLoqControl.Platform.Tests;

public sealed class ProfileStoreTests
{
    [Fact]
    public async Task Missing_store_loads_as_an_empty_collection()
    {
        using var temporary = new TemporaryDirectory();
        var store = new JsonProfileStore(temporary.FilePath);

        IReadOnlyList<HardwareProfile> profiles =
            await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(profiles);
        Assert.False(File.Exists(temporary.FilePath));
    }

    [Fact]
    public async Task Profiles_round_trip_as_versioned_string_enums()
    {
        using var temporary = new TemporaryDirectory();
        var store = new JsonProfileStore(temporary.FilePath);
        var profile = new HardwareProfile(
            new ProfileId(Guid.Parse("28d498cb-92a9-4c77-bf3e-b56f6cfa2138")),
            "Quiet battery",
            new HardwareProfileTargets(
                BatteryChargeMode.Conservation,
                ThermalMode.Quiet));

        await store.SaveAsync(profile, TestContext.Current.CancellationToken);
        IReadOnlyList<HardwareProfile> loaded =
            await store.LoadAsync(TestContext.Current.CancellationToken);
        string json = await File.ReadAllTextAsync(
            temporary.FilePath,
            TestContext.Current.CancellationToken);

        Assert.Equal(profile, Assert.Single(loaded));
        Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"batteryChargeMode\": \"conservation\"", json, StringComparison.Ordinal);
        Assert.Contains("\"thermalMode\": \"quiet\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"batteryChargeMode\": 1", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_an_existing_id_replaces_instead_of_duplicates()
    {
        using var temporary = new TemporaryDirectory();
        var store = new JsonProfileStore(temporary.FilePath);
        ProfileId id = ProfileId.New();
        var first = new HardwareProfile(
            id,
            "First",
            new HardwareProfileTargets(thermalMode: ThermalMode.Balanced));
        var replacement = new HardwareProfile(
            id,
            "Replacement",
            new HardwareProfileTargets(thermalMode: ThermalMode.Performance));

        await store.SaveAsync(first, TestContext.Current.CancellationToken);
        await store.SaveAsync(replacement, TestContext.Current.CancellationToken);
        HardwareProfile loaded = Assert.Single(
            await store.LoadAsync(TestContext.Current.CancellationToken));

        Assert.Equal(replacement, loaded);
    }

    [Fact]
    public async Task Delete_reports_whether_a_profile_existed()
    {
        using var temporary = new TemporaryDirectory();
        var store = new JsonProfileStore(temporary.FilePath);
        HardwareProfile profile = CreateProfile("Delete me", ThermalMode.Quiet);
        await store.SaveAsync(profile, TestContext.Current.CancellationToken);

        bool deleted = await store.DeleteAsync(
            profile.Id,
            TestContext.Current.CancellationToken);
        bool deletedAgain = await store.DeleteAsync(
            profile.Id,
            TestContext.Current.CancellationToken);

        Assert.True(deleted);
        Assert.False(deletedAgain);
        Assert.Empty(await store.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Unknown_json_members_are_rejected()
    {
        const string json =
            """{"schemaVersion":1,"profiles":[],"unexpected":true}""";

        ProfileStoreException exception = await LoadInvalidAsync(json);

        Assert.Equal("profile_store_invalid_json", exception.ErrorCode);
    }

    [Fact]
    public async Task Numeric_hardware_modes_are_rejected()
    {
        string json =
            $$"""
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "id": "{{Guid.NewGuid():D}}",
                  "name": "Numeric enum",
                  "targets": { "batteryChargeMode": 1 }
                }
              ]
            }
            """;

        ProfileStoreException exception = await LoadInvalidAsync(json);

        Assert.Equal("profile_store_invalid_json", exception.ErrorCode);
    }

    [Fact]
    public async Task Unsupported_schema_is_reported_separately()
    {
        const string json = """{"schemaVersion":2,"profiles":[]}""";

        ProfileStoreException exception = await LoadInvalidAsync(json);

        Assert.Equal("profile_store_schema_unsupported", exception.ErrorCode);
    }

    [Fact]
    public async Task Invalid_or_duplicate_profiles_are_rejected()
    {
        Guid id = Guid.NewGuid();
        string json =
            $$"""
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "id": "{{id:D}}",
                  "name": "First",
                  "targets": { "thermalMode": "quiet" }
                },
                {
                  "id": "{{id:D}}",
                  "name": "Duplicate",
                  "targets": { "thermalMode": "balanced" }
                }
              ]
            }
            """;

        ProfileStoreException exception = await LoadInvalidAsync(json);

        Assert.Equal("profile_store_invalid_document", exception.ErrorCode);
    }

    [Fact]
    public async Task Null_profile_is_rejected_as_a_corrupt_document()
    {
        const string json = """{"schemaVersion":1,"profiles":[null]}""";

        ProfileStoreException exception = await LoadInvalidAsync(json);

        Assert.Equal("profile_store_invalid_document", exception.ErrorCode);
    }

    [Fact]
    public async Task Empty_target_set_is_rejected()
    {
        string json =
            $$"""
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "id": "{{Guid.NewGuid():D}}",
                  "name": "No targets",
                  "targets": {}
                }
              ]
            }
            """;

        ProfileStoreException exception = await LoadInvalidAsync(json);

        Assert.Equal("profile_store_invalid_document", exception.ErrorCode);
    }

    [Fact]
    public async Task Oversized_store_is_rejected_before_json_parsing()
    {
        using var temporary = new TemporaryDirectory();
        await File.WriteAllTextAsync(
            temporary.FilePath,
            new string(' ', checked((int)JsonProfileStore.MaximumFileBytes + 1)),
            TestContext.Current.CancellationToken);
        var store = new JsonProfileStore(temporary.FilePath);

        ProfileStoreException exception = await Assert.ThrowsAsync<ProfileStoreException>(
            () => store.LoadAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("profile_store_file_too_large", exception.ErrorCode);
    }

    [Fact]
    public async Task Concurrent_saves_on_one_store_preserve_every_profile()
    {
        using var temporary = new TemporaryDirectory();
        var store = new JsonProfileStore(temporary.FilePath);
        HardwareProfile[] profiles =
        [
            CreateProfile("Charlie", ThermalMode.Performance),
            CreateProfile("Alpha", ThermalMode.Quiet),
            CreateProfile("Bravo", ThermalMode.Balanced),
        ];

        await Task.WhenAll(profiles.Select(profile =>
            store.SaveAsync(profile, TestContext.Current.CancellationToken).AsTask()));
        IReadOnlyList<HardwareProfile> loaded =
            await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Alpha", "Bravo", "Charlie"], loaded.Select(item => item.Name));
        Assert.Empty(Directory.GetFiles(temporary.Path, "*.tmp"));
    }

    [Fact]
    public async Task Concurrent_saves_across_store_instances_preserve_every_profile()
    {
        using var temporary = new TemporaryDirectory();
        JsonProfileStore[] stores =
        [
            new JsonProfileStore(temporary.FilePath),
            new JsonProfileStore(temporary.FilePath),
        ];
        HardwareProfile[] profiles = Enumerable
            .Range(0, 16)
            .Select(index => CreateProfile(
                $"Profile {index:D2}",
                (ThermalMode)(index % 3)))
            .ToArray();

        await Task.WhenAll(profiles.Select((profile, index) =>
            stores[index % stores.Length]
                .SaveAsync(profile, TestContext.Current.CancellationToken)
                .AsTask()));
        IReadOnlyList<HardwareProfile> loaded =
            await new JsonProfileStore(temporary.FilePath)
                .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            profiles.Select(static profile => profile.Name).Order(),
            loaded.Select(static profile => profile.Name));
        Assert.Empty(Directory.GetFiles(temporary.Path, "*.tmp"));
    }

    [Fact]
    public async Task Pre_cancelled_save_does_not_create_a_store()
    {
        using var temporary = new TemporaryDirectory();
        var store = new JsonProfileStore(temporary.FilePath);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store
                .SaveAsync(CreateProfile("Cancelled", ThermalMode.Quiet), cancellation.Token)
                .AsTask());

        Assert.False(File.Exists(temporary.FilePath));
    }

    private static HardwareProfile CreateProfile(string name, ThermalMode thermalMode) =>
        new(
            ProfileId.New(),
            name,
            new HardwareProfileTargets(thermalMode: thermalMode));

    private static async Task<ProfileStoreException> LoadInvalidAsync(string json)
    {
        using var temporary = new TemporaryDirectory();
        await File.WriteAllTextAsync(
            temporary.FilePath,
            json,
            TestContext.Current.CancellationToken);
        var store = new JsonProfileStore(temporary.FilePath);

        return await Assert.ThrowsAsync<ProfileStoreException>(
            () => store.LoadAsync(TestContext.Current.CancellationToken).AsTask());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "LegionLoqControl.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            FilePath = System.IO.Path.Combine(Path, "profiles.v1.json");
        }

        public string Path { get; }

        public string FilePath { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
    }
}

