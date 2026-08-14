using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegionLoqControl.Application.Automation;
using LegionLoqControl.Domain.Automation;
using LegionLoqControl.Domain.Profiles;
using LegionLoqControl.Infrastructure.Windows.Storage;

namespace LegionLoqControl.Infrastructure.Windows.Automation;

public sealed class JsonAutomationRuleStore : IAutomationRuleStore
{
    internal const int CurrentSchemaVersion = 1;
    internal const int MaximumRuleCount = 128;
    internal const long MaximumFileBytes = 256 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly string _filePath;
    private readonly CrossProcessFileLock _fileLock;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonAutomationRuleStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _filePath = Path.GetFullPath(filePath);
        if (string.IsNullOrWhiteSpace(Path.GetFileName(_filePath)))
            throw new ArgumentException("An automation rule store path must include a file name.", nameof(filePath));

        _fileLock = new CrossProcessFileLock(_filePath);
    }

    public static JsonAutomationRuleStore CreateDefault()
    {
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            throw new AutomationRuleStoreException("automation_rule_store_location_unavailable");

        return new JsonAutomationRuleStore(
            Path.Combine(localAppData, "LegionLoqControl", "automation-rules.v1.json"));
    }

    public async ValueTask<IReadOnlyList<AutomationRule>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
                return Array.Empty<AutomationRule>();

            await using FileStream processLock = await _fileLock
                .AcquireAsync(cancellationToken)
                .ConfigureAwait(false);
            return await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (AutomationRuleStoreException)
        {
            throw;
        }
        catch (CrossProcessFileLockUnavailableException exception)
        {
            throw new AutomationRuleStoreException(
                "automation_rule_store_busy",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new AutomationRuleStoreException(
                "automation_rule_store_access_denied",
                exception);
        }
        catch (IOException exception)
        {
            throw new AutomationRuleStoreException(
                "automation_rule_store_io_failed",
                exception);
        }
        catch (Exception exception) when (
            exception is NotSupportedException or SecurityException)
        {
            throw new AutomationRuleStoreException(
                "automation_rule_store_location_invalid",
                exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask SaveAsync(
        AutomationRule rule,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string directory = Path.GetDirectoryName(_filePath)
                ?? throw new AutomationRuleStoreException(
                    "automation_rule_store_location_unavailable");
            Directory.CreateDirectory(directory);
            await using FileStream processLock = await _fileLock
                .AcquireAsync(cancellationToken)
                .ConfigureAwait(false);

            List<AutomationRule> rules =
                [.. await LoadCoreAsync(cancellationToken).ConfigureAwait(false)];
            int existingIndex = rules.FindIndex(item => item.Id == rule.Id);
            if (existingIndex >= 0)
            {
                rules[existingIndex] = rule;
            }
            else
            {
                if (rules.Count >= MaximumRuleCount)
                {
                    throw new AutomationRuleStoreException(
                        "automation_rule_store_limit_reached");
                }

                rules.Add(rule);
            }

            SortRules(rules);
            await WriteCoreAsync(rules, cancellationToken).ConfigureAwait(false);
        }
        catch (AutomationRuleStoreException)
        {
            throw;
        }
        catch (CrossProcessFileLockUnavailableException exception)
        {
            throw new AutomationRuleStoreException(
                "automation_rule_store_busy",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new AutomationRuleStoreException(
                "automation_rule_store_access_denied",
                exception);
        }
        catch (IOException exception)
        {
            throw new AutomationRuleStoreException(
                "automation_rule_store_io_failed",
                exception);
        }
        catch (Exception exception) when (
            exception is NotSupportedException or SecurityException)
        {
            throw new AutomationRuleStoreException(
                "automation_rule_store_location_invalid",
                exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<bool> DeleteAsync(
        AutomationRuleId id,
        CancellationToken cancellationToken = default)
    {
        if (id.Value == Guid.Empty)
            throw new ArgumentException("An automation rule ID cannot be empty.", nameof(id));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
                return false;

            await using FileStream processLock = await _fileLock
                .AcquireAsync(cancellationToken)
                .ConfigureAwait(false);
            List<AutomationRule> rules =
                [.. await LoadCoreAsync(cancellationToken).ConfigureAwait(false)];
            int removed = rules.RemoveAll(item => item.Id == id);
            if (removed == 0)
                return false;

            await WriteCoreAsync(rules, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (AutomationRuleStoreException)
        {
            throw;
        }
        catch (CrossProcessFileLockUnavailableException exception)
        {
            throw new AutomationRuleStoreException(
                "automation_rule_store_busy",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new AutomationRuleStoreException(
                "automation_rule_store_access_denied",
                exception);
        }
        catch (IOException exception)
        {
            throw new AutomationRuleStoreException(
                "automation_rule_store_io_failed",
                exception);
        }
        catch (Exception exception) when (
            exception is NotSupportedException or SecurityException)
        {
            throw new AutomationRuleStoreException(
                "automation_rule_store_location_invalid",
                exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask<IReadOnlyList<AutomationRule>> LoadCoreAsync(
        CancellationToken cancellationToken)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<AutomationRule>();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<AutomationRule>();
        }

        await using (stream.ConfigureAwait(false))
        {
            if (stream.Length > MaximumFileBytes)
            {
                throw new AutomationRuleStoreException(
                    "automation_rule_store_file_too_large");
            }

            AutomationRuleDocumentDto? document;
            try
            {
                document = await JsonSerializer
                    .DeserializeAsync<AutomationRuleDocumentDto>(
                        stream,
                        SerializerOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                throw new AutomationRuleStoreException(
                    "automation_rule_store_invalid_json",
                    exception);
            }

            if (document is null || document.Rules is null)
            {
                throw new AutomationRuleStoreException(
                    "automation_rule_store_invalid_document");
            }

            if (document.SchemaVersion != CurrentSchemaVersion)
            {
                throw new AutomationRuleStoreException(
                    "automation_rule_store_schema_unsupported");
            }

            if (document.Rules.Count > MaximumRuleCount)
            {
                throw new AutomationRuleStoreException(
                    "automation_rule_store_limit_exceeded");
            }

            try
            {
                var ids = new HashSet<AutomationRuleId>();
                var rules = new List<AutomationRule>(document.Rules.Count);
                foreach (AutomationRuleDto item in document.Rules)
                {
                    if (item is null)
                        throw new InvalidDataException("Null automation rule.");
                    if (!Guid.TryParseExact(item.Id, "D", out Guid parsedId))
                        throw new InvalidDataException("Invalid automation rule ID.");
                    if (!Guid.TryParseExact(item.TargetProfileId, "D", out Guid parsedProfileId))
                        throw new InvalidDataException("Invalid target profile ID.");
                    if (!item.RequiredPowerSource.HasValue ||
                        !item.Priority.HasValue ||
                        !item.Enabled.HasValue)
                        throw new InvalidDataException("Missing automation rule value.");

                    var id = new AutomationRuleId(parsedId);
                    if (!ids.Add(id))
                        throw new InvalidDataException("Duplicate automation rule ID.");

                    rules.Add(new AutomationRule(
                        id,
                        item.Name ?? string.Empty,
                        new ProfileId(parsedProfileId),
                        item.RequiredPowerSource.Value,
                        item.Priority.Value,
                        item.Enabled.Value));
                }

                SortRules(rules);
                return Array.AsReadOnly(rules.ToArray());
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidDataException)
            {
                throw new AutomationRuleStoreException(
                    "automation_rule_store_invalid_document",
                    exception);
            }
        }
    }

    private async ValueTask WriteCoreAsync(
        IReadOnlyList<AutomationRule> rules,
        CancellationToken cancellationToken)
    {
        var document = new AutomationRuleDocumentDto
        {
            SchemaVersion = CurrentSchemaVersion,
            Rules = rules.Select(AutomationRuleDto.FromDomain).ToList(),
        };
        byte[] content = JsonSerializer.SerializeToUtf8Bytes(document, SerializerOptions);
        if (content.LongLength > MaximumFileBytes)
        {
            throw new AutomationRuleStoreException(
                "automation_rule_store_file_too_large");
        }

        string directory = Path.GetDirectoryName(_filePath)
            ?? throw new AutomationRuleStoreException(
                "automation_rule_store_location_unavailable");
        Directory.CreateDirectory(directory);

        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void SortRules(List<AutomationRule> rules) =>
        rules.Sort(static (left, right) =>
        {
            int byPriority = right.Priority.CompareTo(left.Priority);
            if (byPriority != 0)
                return byPriority;

            int byName = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            return byName != 0 ? byName : left.Id.Value.CompareTo(right.Id.Value);
        });

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 6,
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = true,
        };
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    private sealed class AutomationRuleDocumentDto
    {
        public int SchemaVersion { get; init; }

        public List<AutomationRuleDto>? Rules { get; init; }
    }

    private sealed class AutomationRuleDto
    {
        public string? Id { get; init; }

        public string? Name { get; init; }

        public string? TargetProfileId { get; init; }

        public PowerSourceKind? RequiredPowerSource { get; init; }

        public int? Priority { get; init; }

        public bool? Enabled { get; init; }

        public static AutomationRuleDto FromDomain(AutomationRule rule) =>
            new()
            {
                Id = rule.Id.ToString(),
                Name = rule.Name,
                TargetProfileId = rule.ProfileId.ToString(),
                RequiredPowerSource = rule.RequiredPowerSource,
                Priority = rule.Priority,
                Enabled = rule.IsEnabled,
            };
    }
}
