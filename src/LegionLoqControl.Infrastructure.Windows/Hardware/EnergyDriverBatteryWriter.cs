using System.ComponentModel;
using System.Runtime.InteropServices;
using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Domain.Controls;
using Microsoft.Win32.SafeHandles;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal interface IEnergyDriverBatteryWriter
{
    ValueTask WriteAsync(
        BatteryChargeMode expected,
        BatteryChargeMode desired,
        CancellationToken cancellationToken);
}

internal sealed class EnergyDriverBatteryWriter : IEnergyDriverBatteryWriter
{
    private const string DevicePath = @"\\.\EnergyDrv";
    private const uint BatteryChargeModeControlCode = 0x831020F8;
    private const uint ConservationOnSelector = 0x03;
    private const uint ConservationOffSelector = 0x05;
    private const uint RapidChargeOnSelector = 0x07;
    private const uint RapidChargeOffSelector = 0x08;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const int ErrorInvalidFunction = 1;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorNotSupported = 50;
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(5);

    internal static uint ControlCodeForValidation => BatteryChargeModeControlCode;

    internal static uint DesiredAccessForValidation => GenericRead | GenericWrite;

    public async ValueTask WriteAsync(
        BatteryChargeMode expected,
        BatteryChargeMode desired,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        uint selector = ResolveSelector(expected, desired);
        try
        {
            await Task
                .Run(() => WriteCore(selector), CancellationToken.None)
                .WaitAsync(WriteTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            throw new HardwareWriteException(
                "energy_driver_write_timed_out",
                HardwareWriteStatus.Failed);
        }
        catch (HardwareWriteException)
        {
            throw;
        }
        catch (Win32Exception exception)
        {
            throw MapWin32Error(exception.NativeErrorCode);
        }
        catch (Exception)
        {
            throw new HardwareWriteException(
                "energy_driver_write_failed",
                HardwareWriteStatus.Failed);
        }
    }

    internal static uint ResolveSelector(BatteryChargeMode expected, BatteryChargeMode desired)
    {
        if (!Enum.IsDefined(expected) || !Enum.IsDefined(desired))
            throw new HardwareWriteException("battery_value_invalid", HardwareWriteStatus.Failed);
        if (expected == desired)
            throw new HardwareWriteException("battery_value_unchanged", HardwareWriteStatus.Failed);

        return desired switch
        {
            BatteryChargeMode.Conservation => ConservationOnSelector,
            BatteryChargeMode.RapidCharge => RapidChargeOnSelector,
            BatteryChargeMode.Normal when expected == BatteryChargeMode.Conservation =>
                ConservationOffSelector,
            BatteryChargeMode.Normal when expected == BatteryChargeMode.RapidCharge =>
                RapidChargeOffSelector,
            _ => throw new HardwareWriteException(
                "battery_normal_selector_unknown",
                HardwareWriteStatus.Failed),
        };
    }

    private static HardwareWriteException MapWin32Error(int errorCode) =>
        errorCode switch
        {
            ErrorFileNotFound or ErrorPathNotFound =>
                new("energy_driver_not_available", HardwareWriteStatus.Failed),
            ErrorAccessDenied =>
                new("energy_driver_access_denied", HardwareWriteStatus.Failed),
            ErrorInvalidFunction or ErrorNotSupported =>
                new("energy_battery_setter_not_supported", HardwareWriteStatus.Unsupported),
            _ => new("energy_driver_write_failed", HardwareWriteStatus.Failed),
        };

    private static void WriteCore(uint selector)
    {
        using SafeFileHandle handle = CreateFile(
            DevicePath,
            DesiredAccessForValidation,
            FileShareRead | FileShareWrite,
            nint.Zero,
            OpenExisting,
            FileAttributeNormal,
            nint.Zero);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        if (!DeviceIoControl(
                handle,
                BatteryChargeModeControlCode,
                in selector,
                sizeof(uint),
                out _,
                sizeof(uint),
                out uint bytesReturned,
                nint.Zero))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        if (bytesReturned != sizeof(uint))
            throw new InvalidDataException("EnergyDrv returned an invalid battery write payload size.");
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
