using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegionLoqControl.Application.Profiles;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Profiles;

namespace LegionLoqControl.Infrastructure.Windows.Profiles;

public sealed class JsonProfileStore : IProfileStore
{
    internal const int CurrentSchemaVersion = 1;
    internal const int MaximumProfileCount = 64;
    internal const long MaximumFileBytes = 256 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonProfileStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _filePath = Path.GetFullPath(filePath);
        if (string.IsNullOrWhiteSpace(Path.GetFileName(_filePath)))
            throw new ArgumentException("A profile store path must include a file name.", nameof(filePath));
    }

    public static JsonProfileStore CreateDefault()
    {
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            throw new ProfileStoreException("profile_store_location_unavailable");

        return new JsonProfileStore(
            Path.Combine(localAppData, "LegionLoqControl", "profiles.v1.json"));
    }

    public async ValueTask<IReadOnlyList<HardwareProfile>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ProfileStoreException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ProfileStoreException("profile_store_access_denied", exception);
        }
        catch (IOException exception)
        {
            throw new ProfileStoreException("profile_store_io_failed", exception);
        }
        catch (Exception exception) when (
            exception is NotSupportedException or SecurityException)
        {
            throw new ProfileStoreException("profile_store_location_invalid", exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask SaveAsync(
        HardwareProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<HardwareProfile> profiles =
                [.. await LoadCoreAsync(cancellationToken).ConfigureAwait(false)];
            int existingIndex = profiles.FindIndex(item => item.Id == profile.Id);
            if (existingIndex >= 0)
            {
                profiles[existingIndex] = profile;
            }
            else
            {
                if (profiles.Count >= MaximumProfileCount)
                    throw new ProfileStoreException("profile_store_limit_reached");

                profiles.Add(profile);
            }

            SortProfiles(profiles);
            await WriteCoreAsync(profiles, cancellationToken).ConfigureAwait(false);
        }
        catch (ProfileStoreException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ProfileStoreException("profile_store_access_denied", exception);
        }
        catch (IOException exception)
        {
            throw new ProfileStoreException("profile_store_io_failed", exception);
        }
        catch (Exception exception) when (
            exception is NotSupportedException or SecurityException)
        {
            throw new ProfileStoreException("profile_store_location_invalid", exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<bool> DeleteAsync(
        ProfileId id,
        CancellationToken cancellationToken = default)
    {
        if (id.Value == Guid.Empty)
            throw new ArgumentException("A profile ID cannot be empty.", nameof(id));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<HardwareProfile> profiles =
                [.. await LoadCoreAsync(cancellationToken).ConfigureAwait(false)];
            int removed = profiles.RemoveAll(item => item.Id == id);
            if (removed == 0)
                return false;

            await WriteCoreAsync(profiles, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ProfileStoreException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ProfileStoreException("profile_store_access_denied", exception);
        }
        catch (IOException exception)
        {
            throw new ProfileStoreException("profile_store_io_failed", exception);
        }
        catch (Exception exception) when (
            exception is NotSupportedException or SecurityException)
        {
            throw new ProfileStoreException("profile_store_location_invalid", exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask<IReadOnlyList<HardwareProfile>> LoadCoreAsync(
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
            return Array.Empty<HardwareProfile>();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<HardwareProfile>();
        }

        await using (stream.ConfigureAwait(false))
        {
            if (stream.Length > MaximumFileBytes)
                throw new ProfileStoreException("profile_store_file_too_large");

            ProfileDocumentDto? document;
            try
            {
                document = await JsonSerializer
                    .DeserializeAsync<ProfileDocumentDto>(
                        stream,
                        SerializerOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                throw new ProfileStoreException("profile_store_invalid_json", exception);
            }

            if (document is null || document.Profiles is null)
                throw new ProfileStoreException("profile_store_invalid_document");
            if (document.SchemaVersion != CurrentSchemaVersion)
                throw new ProfileStoreException("profile_store_schema_unsupported");
            if (document.Profiles.Count > MaximumProfileCount)
                throw new ProfileStoreException("profile_store_limit_exceeded");

            try
            {
                var ids = new HashSet<ProfileId>();
                var profiles = new List<HardwareProfile>(document.Profiles.Count);
                foreach (ProfileDto item in document.Profiles)
                {
                    if (item is null)
                        throw new InvalidDataException("Null profile.");
                    if (!Guid.TryParseExact(item.Id, "D", out Guid parsedId))
                        throw new InvalidDataException("Invalid profile ID.");
                    if (item.Targets is null)
                        throw new InvalidDataException("Missing profile targets.");

                    var id = new ProfileId(parsedId);
                    if (!ids.Add(id))
                        throw new InvalidDataException("Duplicate profile ID.");

                    var targets = new HardwareProfileTargets(
                        item.Targets.BatteryChargeMode,
                        item.Targets.ThermalMode);
                    profiles.Add(new HardwareProfile(id, item.Name ?? string.Empty, targets));
                }

                SortProfiles(profiles);
                return Array.AsReadOnly(profiles.ToArray());
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidDataException)
            {
                throw new ProfileStoreException("profile_store_invalid_document", exception);
            }
        }
    }

    private async ValueTask WriteCoreAsync(
        IReadOnlyList<HardwareProfile> profiles,
        CancellationToken cancellationToken)
    {
        var document = new ProfileDocumentDto
        {
            SchemaVersion = CurrentSchemaVersion,
            Profiles = profiles.Select(ProfileDto.FromDomain).ToList(),
        };
        byte[] content = JsonSerializer.SerializeToUtf8Bytes(document, SerializerOptions);
        if (content.LongLength > MaximumFileBytes)
            throw new ProfileStoreException("profile_store_file_too_large");

        string directory = Path.GetDirectoryName(_filePath)
            ?? throw new ProfileStoreException("profile_store_location_unavailable");
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

    private static void SortProfiles(List<HardwareProfile> profiles) =>
        profiles.Sort(static (left, right) =>
        {
            int byName = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            return byName != 0 ? byName : left.Id.Value.CompareTo(right.Id.Value);
        });

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 8,
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

    private sealed class ProfileDocumentDto
    {
        public int SchemaVersion { get; init; }

        public List<ProfileDto>? Profiles { get; init; }
    }

    private sealed class ProfileDto
    {
        public string? Id { get; init; }

        public string? Name { get; init; }

        public ProfileTargetsDto? Targets { get; init; }

        public static ProfileDto FromDomain(HardwareProfile profile) =>
            new()
            {
                Id = profile.Id.ToString(),
                Name = profile.Name,
                Targets = new ProfileTargetsDto
                {
                    BatteryChargeMode = profile.Targets.BatteryChargeMode,
                    ThermalMode = profile.Targets.ThermalMode,
                },
            };
    }

    private sealed class ProfileTargetsDto
    {
        public BatteryChargeMode? BatteryChargeMode { get; init; }

        public ThermalMode? ThermalMode { get; init; }
    }
}

