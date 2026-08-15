using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Domain.Diagnostics;

public readonly record struct MemoryTelemetry(ulong TotalBytes, ulong UsedBytes)
{
    public ulong AvailableBytes => TotalBytes >= UsedBytes ? TotalBytes - UsedBytes : 0;
}

public readonly record struct DiskTelemetry(string Root, ulong TotalBytes, ulong UsedBytes)
{
    public ulong FreeBytes => TotalBytes >= UsedBytes ? TotalBytes - UsedBytes : 0;
}

public readonly record struct SystemResourceTelemetry(
    HardwareReadResult<byte> CpuPercent,
    HardwareReadResult<MemoryTelemetry> Memory,
    HardwareReadResult<DiskTelemetry> Disk)
{
    public static SystemResourceTelemetry Unavailable { get; } = new(
        HardwareReadResult<byte>.Failure(HardwareReadStatus.Unavailable, "cpu_not_sampled"),
        HardwareReadResult<MemoryTelemetry>.Failure(
            HardwareReadStatus.Failed,
            "memory_status_api_failed"),
        HardwareReadResult<DiskTelemetry>.Failure(
            HardwareReadStatus.Failed,
            "disk_space_api_failed"));
}
