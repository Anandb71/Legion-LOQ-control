using LegionLoqControl.Application.Automation;
using LegionLoqControl.Domain.Automation;
using LegionLoqControl.Domain.Profiles;
using LegionLoqControl.Infrastructure.Windows.Automation;
using Xunit;

namespace LegionLoqControl.Platform.Tests;

public sealed class AutomationRuleStoreTests
{
    [Fact]
    public async Task Missing_store_loads_as_an_empty_collection()
    {
        using var temporary = new TemporaryDirectory();
        var store = new JsonAutomationRuleStore(temporary.FilePath);

        IReadOnlyList<AutomationRule> rules =
            await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(rules);
        Assert.False(File.Exists(temporary.FilePath));
    }

    [Fact]
    public async Task Rules_round_trip_as_versioned_string_enums()
    {
        using var temporary = new TemporaryDirectory();
        var store = new JsonAutomationRuleStore(temporary.FilePath);
        var rule = new AutomationRule(
            new AutomationRuleId(Guid.Parse("236ca38f-1e6f-42c6-9bdb-bb004854a05e")),
            "Battery quiet",
            new ProfileId(Guid.Parse("a378672c-d96b-4f52-94f8-ab621c51869f")),
            PowerSourceKind.Battery,
            priority: 40,
            isEnabled: true);

        await store.SaveAsync(rule, TestContext.Current.CancellationToken);
        IReadOnlyList<AutomationRule> loaded =
            await store.LoadAsync(TestContext.Current.CancellationToken);
        string json = await File.ReadAllTextAsync(
            temporary.FilePath,
            TestContext.Current.CancellationToken);

        Assert.Equal(rule, Assert.Single(loaded));
        Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"requiredPowerSource\": \"battery\"", json, StringComparison.Ordinal);
        Assert.Contains("\"priority\": 40", json, StringComparison.Ordinal);
        Assert.Contains("\"enabled\": true", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"requiredPowerSource\": 1", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_an_existing_id_replaces_instead_of_duplicates()
    {
        using var temporary = new TemporaryDirectory();
        var store = new JsonAutomationRuleStore(temporary.FilePath);
        AutomationRuleId id = AutomationRuleId.New();
        ProfileId profileId = ProfileId.New();
        var first = new AutomationRule(
            id,
            "First",
            profileId,
            PowerSourceKind.Ac,
            priority: 10,
            isEnabled: true);
        var replacement = new AutomationRule(
            id,
            "Replacement",
            profileId,
            PowerSourceKind.Battery,
            priority: 90,
            isEnabled: false);

        await store.SaveAsync(first, TestContext.Current.CancellationToken);
        await store.SaveAsync(replacement, TestContext.Current.CancellationToken);
        AutomationRule loaded = Assert.Single(
            await store.LoadAsync(TestContext.Current.CancellationToken));

        Assert.Equal(replacement, loaded);
    }

    [Fact]
    public async Task Delete_reports_whether_a_rule_existed()
    {
        using var temporary = new TemporaryDirectory();
        var store = new JsonAutomationRuleStore(temporary.FilePath);
        AutomationRule rule = CreateRule("Delete me", priority: 20);
        await store.SaveAsync(rule, TestContext.Current.CancellationToken);

        bool deleted = await store.DeleteAsync(
            rule.Id,
            TestContext.Current.CancellationToken);
        bool deletedAgain = await store.DeleteAsync(
            rule.Id,
            TestContext.Current.CancellationToken);

        Assert.True(deleted);
        Assert.False(deletedAgain);
        Assert.Empty(await store.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Unknown_json_members_are_rejected()
    {
        const string json =
            """{"schemaVersion":1,"rules":[],"unexpected":true}""";

        AutomationRuleStoreException exception = await LoadInvalidAsync(json);

        Assert.Equal("automation_rule_store_invalid_json", exception.ErrorCode);
    }

    [Fact]
    public async Task Numeric_power_sources_are_rejected()
    {
        string json =
            $$"""
            {
              "schemaVersion": 1,
              "rules": [
                {
                  "id": "{{Guid.NewGuid():D}}",
                  "name": "Numeric enum",
                  "targetProfileId": "{{Guid.NewGuid():D}}",
                  "requiredPowerSource": 1,
                  "priority": 10,
                  "enabled": true
                }
              ]
            }
            """;

        AutomationRuleStoreException exception = await LoadInvalidAsync(json);

        Assert.Equal("automation_rule_store_invalid_json", exception.ErrorCode);
    }

    [Fact]
    public async Task Unsupported_schema_is_reported_separately()
    {
        const string json = """{"schemaVersion":2,"rules":[]}""";

        AutomationRuleStoreException exception = await LoadInvalidAsync(json);

        Assert.Equal("automation_rule_store_schema_unsupported", exception.ErrorCode);
    }

    [Fact]
    public async Task Invalid_or_duplicate_rules_are_rejected()
    {
        Guid id = Guid.NewGuid();
        string json =
            $$"""
            {
              "schemaVersion": 1,
              "rules": [
                {
                  "id": "{{id:D}}",
                  "name": "First",
                  "targetProfileId": "{{Guid.NewGuid():D}}",
                  "priority": 10,
                  "enabled": true
                },
                {
                  "id": "{{id:D}}",
                  "name": "Duplicate",
                  "targetProfileId": "{{Guid.NewGuid():D}}",
                  "priority": 20,
                  "enabled": true
                }
              ]
            }
            """;

        AutomationRuleStoreException exception = await LoadInvalidAsync(json);

        Assert.Equal("automation_rule_store_invalid_document", exception.ErrorCode);
    }

    [Theory]
    [InlineData("priority")]
    [InlineData("enabled")]
    public async Task Missing_required_rule_values_are_rejected(string omittedProperty)
    {
        string remainingProperty = omittedProperty == "priority"
            ? "\"enabled\": true"
            : "\"priority\": 10";
        string json =
            $$"""
            {
              "schemaVersion": 1,
              "rules": [
                {
                  "id": "{{Guid.NewGuid():D}}",
                  "name": "Incomplete",
                  "targetProfileId": "{{Guid.NewGuid():D}}",
                  "requiredPowerSource": "ac",
                  {{remainingProperty}}
                }
              ]
            }
            """;

        AutomationRuleStoreException exception = await LoadInvalidAsync(json);

        Assert.Equal("automation_rule_store_invalid_document", exception.ErrorCode);
    }

    [Fact]
    public async Task Null_rule_is_rejected_as_a_corrupt_document()
    {
        const string json = """{"schemaVersion":1,"rules":[null]}""";

        AutomationRuleStoreException exception = await LoadInvalidAsync(json);

        Assert.Equal("automation_rule_store_invalid_document", exception.ErrorCode);
    }

    [Fact]
    public async Task Oversized_store_is_rejected_before_json_parsing()
    {
        using var temporary = new TemporaryDirectory();
        await File.WriteAllTextAsync(
            temporary.FilePath,
            new string(' ', checked((int)JsonAutomationRuleStore.MaximumFileBytes + 1)),
            TestContext.Current.CancellationToken);
        var store = new JsonAutomationRuleStore(temporary.FilePath);

        AutomationRuleStoreException exception =
            await Assert.ThrowsAsync<AutomationRuleStoreException>(
                () => store.LoadAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("automation_rule_store_file_too_large", exception.ErrorCode);
    }

    [Fact]
    public async Task Concurrent_saves_preserve_rules_in_deterministic_order()
    {
        using var temporary = new TemporaryDirectory();
        var store = new JsonAutomationRuleStore(temporary.FilePath);
        AutomationRule[] rules =
        [
            CreateRule("Charlie", priority: 10),
            CreateRule("Alpha", priority: 30),
            CreateRule("Bravo", priority: 30),
        ];

        await Task.WhenAll(rules.Select(rule =>
            store.SaveAsync(rule, TestContext.Current.CancellationToken).AsTask()));
        IReadOnlyList<AutomationRule> loaded =
            await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Alpha", "Bravo", "Charlie"], loaded.Select(item => item.Name));
        Assert.Empty(Directory.GetFiles(temporary.Path, "*.tmp"));
    }

    [Fact]
    public async Task Pre_cancelled_save_does_not_create_a_store()
    {
        using var temporary = new TemporaryDirectory();
        var store = new JsonAutomationRuleStore(temporary.FilePath);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store
                .SaveAsync(CreateRule("Cancelled", priority: 10), cancellation.Token)
                .AsTask());

        Assert.False(File.Exists(temporary.FilePath));
    }

    private static AutomationRule CreateRule(string name, int priority) =>
        new(
            AutomationRuleId.New(),
            name,
            ProfileId.New(),
            PowerSourceKind.Ac,
            priority,
            isEnabled: true);

    private static async Task<AutomationRuleStoreException> LoadInvalidAsync(string json)
    {
        using var temporary = new TemporaryDirectory();
        await File.WriteAllTextAsync(
            temporary.FilePath,
            json,
            TestContext.Current.CancellationToken);
        var store = new JsonAutomationRuleStore(temporary.FilePath);

        return await Assert.ThrowsAsync<AutomationRuleStoreException>(
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
            FilePath = System.IO.Path.Combine(Path, "automation-rules.v1.json");
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
