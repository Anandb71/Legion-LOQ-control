using System.ComponentModel;
using System.Runtime.InteropServices;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Results;
using Microsoft.Win32.SafeHandles;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal interface IEnergyDriverBatteryReader
{
    ValueTask<HardwareReadResult<BatteryChargeMode>> ReadAsync(
        CancellationToken cancellationToken);
}

internal sealed class EnergyDriverBatteryReader : IEnergyDriverBatteryReader
{
    private const string DevicePath = @"\\.\EnergyDrv";
    private const uint BatteryChargeModeControlCode = 0x831020F8;
    private const uint ReadSelector = 0xFF;
    private const uint ConservationModeBit = 0x20;
    private const uint RapidChargeBit = 0x04;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const int ErrorInvalidFunction = 1;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorNotSupported = 50;
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    internal static uint ControlCodeForValidation => BatteryChargeModeControlCode;

    internal static uint ReadSelectorForValidation => ReadSelector;

    internal static uint DesiredAccessForValidation => 0;

    public async ValueTask<HardwareReadResult<BatteryChargeMode>> ReadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            uint rawValue = await Task
                .Run(ReadCore, CancellationToken.None)
                .WaitAsync(ReadTimeout, cancellationToken)
                .ConfigureAwait(false);
            return MapRawValue(rawValue);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return HardwareReadResult<BatteryChargeMode>.Failure(
                HardwareReadStatus.TimedOut,
                "energy_driver_read_timed_out");
        }
        catch (Win32Exception exception)
        {
            return MapWin32Error(exception.NativeErrorCode);
        }
        catch (Exception)
        {
            return HardwareReadResult<BatteryChargeMode>.Failure(
                HardwareReadStatus.Failed,
                "energy_driver_read_failed");
        }
    }

    internal static HardwareReadResult<BatteryChargeMode> MapRawValue(uint rawValue)
    {
        bool conservation = (rawValue & ConservationModeBit) != 0;
        bool rapidCharge = (rawValue & RapidChargeBit) != 0;
        if (conservation && rapidCharge)
        {
            return HardwareReadResult<BatteryChargeMode>.Failure(
                HardwareReadStatus.InvalidData,
                "energy_battery_mode_conflict");
        }

        BatteryChargeMode mode = conservation
            ? BatteryChargeMode.Conservation
            : rapidCharge
                ? BatteryChargeMode.RapidCharge
                : BatteryChargeMode.Normal;
        return HardwareReadResult<BatteryChargeMode>.Success(mode);
    }

    private static HardwareReadResult<BatteryChargeMode> MapWin32Error(int errorCode) =>
        errorCode switch
        {
            ErrorFileNotFound or ErrorPathNotFound =>
                HardwareReadResult<BatteryChargeMode>.Failure(
                    HardwareReadStatus.Unavailable,
                    "energy_driver_not_available"),
            ErrorAccessDenied =>
                HardwareReadResult<BatteryChargeMode>.Failure(
                    HardwareReadStatus.AccessDenied,
                    "energy_driver_access_denied"),
            ErrorInvalidFunction or ErrorNotSupported =>
                HardwareReadResult<BatteryChargeMode>.Failure(
                    HardwareReadStatus.Unsupported,
                    "energy_battery_getter_not_supported"),
            _ => HardwareReadResult<BatteryChargeMode>.Failure(
                HardwareReadStatus.Failed,
                "energy_driver_read_failed"),
        };

    private static uint ReadCore()
    {
        using SafeFileHandle handle = CreateFile(
            DevicePath,
            desiredAccess: 0,
            FileShareRead | FileShareWrite,
            nint.Zero,
            OpenExisting,
            FileAttributeNormal,
            nint.Zero);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        uint input = ReadSelector;
        if (!DeviceIoControl(
                handle,
                BatteryChargeModeControlCode,
                in input,
                sizeof(uint),
                out uint output,
                sizeof(uint),
                out uint bytesReturned,
                nint.Zero))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        if (bytesReturned != sizeof(uint))
            throw new InvalidDataException("EnergyDrv returned an invalid battery payload size.");

        return output;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFile(
        [MarshalAs(UnmanagedType.LPWStr)] string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        in uint input,
        uint inputSize,
        out uint output,
        uint outputSize,
        out uint bytesReturned,
        nint overlapped);
}
