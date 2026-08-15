using System.Text.Json;
using LegionLoqControl.Domain.Controls;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

public sealed class OemFanTableStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly string _path;

    public OemFanTableStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LegionLoqControl",
            "oem-fan-table.json");
    }

    public FanTableSnapshot? TryRead()
    {
        if (!File.Exists(_path))
            return null;

        try
        {
            string json = File.ReadAllText(_path);
            StoredFanTable? stored = JsonSerializer.Deserialize<StoredFanTable>(json, SerializerOptions);
            if (stored?.Points is not { Length: > 0 and <= FanTableSnapshot.MaximumPoints })
                return null;

            return new FanTableSnapshot(stored.FanId, stored.SensorId, stored.Points);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void SaveIfAbsent(FanTableSnapshot snapshot)
    {
        if (snapshot.PointCount is < 1 or > FanTableSnapshot.MaximumPoints ||
            File.Exists(_path))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var stored = new StoredFanTable(snapshot.FanId, snapshot.SensorId, snapshot.Points);
            File.WriteAllText(_path, JsonSerializer.Serialize(stored, SerializerOptions));
        }
        catch (Exception)
        {
        }
    }

    private sealed record StoredFanTable(byte FanId, byte SensorId, FanTablePoint[] Points);
}
