namespace LegionLoqControl.Application.Hardware;

public sealed record HardwareWriteJournalEntry(
    DateTimeOffset AtUtc,
    HardwareWriteKind Kind,
    string Expected,
    string Desired,
    HardwareWriteStatus Status,
    string? ErrorCode);

public interface IHardwareWriteJournal
{
    void Append(HardwareWriteJournalEntry entry);

    IReadOnlyList<HardwareWriteJournalEntry> Snapshot();
}

public sealed class HardwareWriteJournal : IHardwareWriteJournal
{
    public const int MaximumEntries = 32;

    private readonly object _sync = new();
    private readonly List<HardwareWriteJournalEntry> _entries = [];

    public void Append(HardwareWriteJournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Expected);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Desired);
        if (!Enum.IsDefined(entry.Kind) || !Enum.IsDefined(entry.Status))
            throw new ArgumentOutOfRangeException(nameof(entry));
        if (entry.ErrorCode is { Length: > 64 })
            throw new ArgumentOutOfRangeException(nameof(entry));

        lock (_sync)
        {
            _entries.Add(entry);
            if (_entries.Count > MaximumEntries)
                _entries.RemoveRange(0, _entries.Count - MaximumEntries);
        }
    }

    public IReadOnlyList<HardwareWriteJournalEntry> Snapshot()
    {
        lock (_sync)
            return [.. _entries];
    }
}
