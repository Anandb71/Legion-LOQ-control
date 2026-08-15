using System.Globalization;
using System.Runtime.InteropServices;
using LegionLoqControl.Application.Automation;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Diagnostics;

public sealed class WindowsResourceTelemetryReader : ISystemResourceTelemetryReader
{
    private ulong _previousIdle;
    private ulong _previousKernel;
    private ulong _previousUser;
    private bool _hasBaseline;

    public ValueTask<SystemResourceTelemetry> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WindowsPlatform.EnsureSupported();

        HardwareReadResult<byte> cpu = ReadCpu();
        HardwareReadResult<MemoryTelemetry> memory = ReadMemory();
        HardwareReadResult<DiskTelemetry> disk = ReadDisk();
        return ValueTask.FromResult(new SystemResourceTelemetry(cpu, memory, disk));
    }

    internal static HardwareReadResult<byte> MapCpuPercent(
        ulong previousIdle,
        ulong previousKernel,
        ulong previousUser,
        ulong idle,
        ulong kernel,
        ulong user)
    {
        if (idle < previousIdle || kernel < previousKernel || user < previousUser)
        {
            return HardwareReadResult<byte>.Failure(
                HardwareReadStatus.InvalidData,
                "cpu_times_went_backwards");
        }

        ulong deltaIdle = idle - previousIdle;
        ulong deltaKernel = kernel - previousKernel;
        ulong deltaUser = user - previousUser;
        ulong total = deltaKernel + deltaUser;
        if (total == 0)
        {
            return HardwareReadResult<byte>.Failure(
                HardwareReadStatus.Unavailable,
                "cpu_sample_too_small");
        }

        ulong busy = total - Math.Min(deltaIdle, total);
        return HardwareReadResult<byte>.Success((byte)Math.Clamp(
            (int)Math.Round(100d * busy / total, MidpointRounding.AwayFromZero),
            0,
            100));
    }

    internal static HardwareReadResult<MemoryTelemetry> MapMemory(ulong totalBytes, ulong availableBytes)
    {
        if (totalBytes == 0 || availableBytes > totalBytes)
        {
            return HardwareReadResult<MemoryTelemetry>.Failure(
                HardwareReadStatus.InvalidData,
                "memory_status_invalid");
        }

        return HardwareReadResult<MemoryTelemetry>.Success(
            new MemoryTelemetry(totalBytes, totalBytes - availableBytes));
    }

    internal static HardwareReadResult<DiskTelemetry> MapDisk(
        string? root,
        ulong totalBytes,
        ulong freeBytes)
    {
        if (string.IsNullOrWhiteSpace(root) ||
            root.Length > 8 ||
            totalBytes == 0 ||
            freeBytes > totalBytes)
        {
            return HardwareReadResult<DiskTelemetry>.Failure(
                HardwareReadStatus.InvalidData,
                "disk_space_invalid");
        }

        return HardwareReadResult<DiskTelemetry>.Success(
            new DiskTelemetry(root.Trim(), totalBytes, totalBytes - freeBytes));
    }

    internal static string FormatGib(ulong bytes)
    {
        double gib = bytes / 1073741824d;
        string format = gib >= 10 ? "0" : "0.0";
        return gib.ToString(format, CultureInfo.InvariantCulture) + " GB";
    }

    private HardwareReadResult<byte> ReadCpu()
    {
        if (!GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime))
        {
            return HardwareReadResult<byte>.Failure(
                HardwareReadStatus.Failed,
                "cpu_times_api_failed");
        }

        ulong idle = idleTime.ToUInt64();
        ulong kernel = kernelTime.ToUInt64();
        ulong user = userTime.ToUInt64();
        if (!_hasBaseline)
        {
            _previousIdle = idle;
            _previousKernel = kernel;
            _previousUser = user;
            _hasBaseline = true;
            return HardwareReadResult<byte>.Failure(
                HardwareReadStatus.Unavailable,
                "cpu_baseline_pending");
        }

        HardwareReadResult<byte> result = MapCpuPercent(
            _previousIdle,
            _previousKernel,
            _previousUser,
            idle,
            kernel,
            user);
        _previousIdle = idle;
        _previousKernel = kernel;
        _previousUser = user;
        return result;
    }

    private static HardwareReadResult<MemoryTelemetry> ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return HardwareReadResult<MemoryTelemetry>.Failure(
                HardwareReadStatus.Failed,
                "memory_status_api_failed");
        }

        return MapMemory(status.TotalPhys, status.AvailPhys);
    }

    private static HardwareReadResult<DiskTelemetry> ReadDisk()
    {
        string? root = Path.GetPathRoot(Environment.SystemDirectory);
        if (string.IsNullOrWhiteSpace(root))
        {
            return HardwareReadResult<DiskTelemetry>.Failure(
                HardwareReadStatus.Unavailable,
                "disk_root_unavailable");
        }

        if (!GetDiskFreeSpaceEx(root, out ulong _, out ulong totalBytes, out ulong freeBytes))
        {
            return HardwareReadResult<DiskTelemetry>.Failure(
                HardwareReadStatus.Failed,
                "disk_space_api_failed");
        }

        string label = root.TrimEnd('\\', '/');
        return MapDisk(label, totalBytes, freeBytes);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out FileTime idleTime,
        out FileTime kernelTime,
        out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(
        string directoryName,
        out ulong freeBytesAvailable,
        out ulong totalNumberOfBytes,
        out ulong totalNumberOfFreeBytes);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;

        public ulong ToUInt64() => ((ulong)HighDateTime << 32) | LowDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}
