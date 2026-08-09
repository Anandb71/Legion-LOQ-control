using System.Runtime.InteropServices;
using LegionLoqControl.Application.Automation;
using LegionLoqControl.Domain.Automation;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Automation;

public sealed class WindowsPowerSourceReader : IPowerSourceReader
{
    public ValueTask<HardwareReadResult<PowerSourceKind>> ReadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WindowsPlatform.EnsureSupported();

        if (!GetSystemPowerStatus(out SystemPowerStatus status))
        {
            return ValueTask.FromResult(
                HardwareReadResult<PowerSourceKind>.Failure(
                    HardwareReadStatus.Failed,
                    "power_status_api_failed"));
        }

        return ValueTask.FromResult(MapAcLineStatus(status.AcLineStatus));
    }

    internal static HardwareReadResult<PowerSourceKind> MapAcLineStatus(
        byte acLineStatus) =>
        acLineStatus switch
        {
            0 => HardwareReadResult<PowerSourceKind>.Success(
                PowerSourceKind.Battery),
            1 => HardwareReadResult<PowerSourceKind>.Success(
                PowerSourceKind.Ac),
            byte.MaxValue => HardwareReadResult<PowerSourceKind>.Failure(
                HardwareReadStatus.Unavailable,
                "power_source_unknown"),
            _ => HardwareReadResult<PowerSourceKind>.Failure(
                HardwareReadStatus.InvalidData,
                "power_source_value_invalid"),
        };

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(
        out SystemPowerStatus systemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }
}

