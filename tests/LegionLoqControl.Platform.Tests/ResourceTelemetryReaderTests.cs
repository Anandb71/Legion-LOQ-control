using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Diagnostics;
using Xunit;

namespace LegionLoqControl.Platform.Tests;

public sealed class ResourceTelemetryReaderTests
{
    [Fact]
    public void Cpu_percent_uses_kernel_plus_user_minus_idle()
    {
        HardwareReadResult<byte> result = WindowsResourceTelemetryReader.MapCpuPercent(
            previousIdle: 0,
            previousKernel: 0,
            previousUser: 0,
            idle: 100,
            kernel: 200,
            user: 50);

        Assert.Equal(HardwareReadStatus.Success, result.Status);
        Assert.Equal((byte)60, result.Value);
    }

    [Fact]
    public void Cpu_percent_is_zero_when_only_idle_advanced()
    {
        HardwareReadResult<byte> result = WindowsResourceTelemetryReader.MapCpuPercent(
            previousIdle: 10,
            previousKernel: 10,
            previousUser: 0,
            idle: 40,
            kernel: 40,
            user: 0);

        Assert.Equal((byte)0, result.Value);
    }

    [Fact]
    public void Cpu_percent_rejects_backwards_counters()
    {
        HardwareReadResult<byte> result = WindowsResourceTelemetryReader.MapCpuPercent(
            previousIdle: 50,
            previousKernel: 80,
            previousUser: 20,
            idle: 10,
            kernel: 80,
            user: 20);

        Assert.Equal(HardwareReadStatus.InvalidData, result.Status);
        Assert.Equal("cpu_times_went_backwards", result.ErrorCode);
    }

    [Fact]
    public void Memory_used_is_total_minus_available()
    {
        HardwareReadResult<Domain.Diagnostics.MemoryTelemetry> result =
            WindowsResourceTelemetryReader.MapMemory(32UL * 1024 * 1024 * 1024, 8UL * 1024 * 1024 * 1024);

        Assert.Equal(HardwareReadStatus.Success, result.Status);
        Assert.Equal(24UL * 1024 * 1024 * 1024, result.Value!.Value.UsedBytes);
        Assert.Equal(32UL * 1024 * 1024 * 1024, result.Value.Value.TotalBytes);
    }

    [Fact]
    public void Memory_rejects_available_above_total()
    {
        HardwareReadResult<Domain.Diagnostics.MemoryTelemetry> result =
            WindowsResourceTelemetryReader.MapMemory(100, 101);

        Assert.Equal(HardwareReadStatus.InvalidData, result.Status);
        Assert.Equal("memory_status_invalid", result.ErrorCode);
    }

    [Fact]
    public void Disk_used_is_total_minus_free()
    {
        HardwareReadResult<Domain.Diagnostics.DiskTelemetry> result =
            WindowsResourceTelemetryReader.MapDisk("C:", 1000, 250);

        Assert.Equal("C:", result.Value!.Value.Root);
        Assert.Equal(750UL, result.Value.Value.UsedBytes);
        Assert.Equal(1000UL, result.Value.Value.TotalBytes);
    }

    [Theory]
    [InlineData(1073741824UL, "1.0 GB")]
    [InlineData(17179869184UL, "16 GB")]
    public void Gib_formatting_stays_short(ulong bytes, string expected)
    {
        Assert.Equal(expected, WindowsResourceTelemetryReader.FormatGib(bytes));
    }

    [Fact]
    public async Task Windows_api_read_returns_typed_memory_and_disk()
    {
        var reader = new WindowsResourceTelemetryReader();

        var first = await reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HardwareReadStatus.Unavailable, first.CpuPercent.Status);
        Assert.Equal("cpu_baseline_pending", first.CpuPercent.ErrorCode);
        Assert.Equal(HardwareReadStatus.Success, first.Memory.Status);
        Assert.Equal(HardwareReadStatus.Success, first.Disk.Status);
        Assert.True(first.Memory.Value!.Value.TotalBytes >= first.Memory.Value.Value.UsedBytes);
        Assert.True(first.Disk.Value!.Value.TotalBytes >= first.Disk.Value.Value.UsedBytes);

        var second = await reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.True(
            second.CpuPercent.Status is HardwareReadStatus.Success or HardwareReadStatus.Unavailable);
        if (second.CpuPercent.Status == HardwareReadStatus.Success)
            Assert.InRange(second.CpuPercent.Value!.Value, (byte)0, (byte)100);
    }
}
