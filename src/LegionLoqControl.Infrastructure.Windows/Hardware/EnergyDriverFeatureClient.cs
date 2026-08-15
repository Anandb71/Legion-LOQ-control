using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Results;
using Microsoft.Win32.SafeHandles;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal sealed class EnergyDriverFeatureClient
{
    private const string DevicePath = @"\\.\EnergyDrv";
    internal const uint SettingsControlCode = 0x831020E8;
    internal const uint NightChargeControlCode = 0x83102150;
    private const uint SettingsReadSelector = 0x2;
    private const uint NightChargeReadSelector = 0x11;
    private const uint NightChargeOn = 0x80000012;
    private const uint NightChargeOff = 0x12;
    private const uint FnLockOn = 0xE;
    private const uint FnLockOff = 0xF;
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
    private static readonly TimeSpan IoTimeout = TimeSpan.FromSeconds(5);

    internal static uint SettingsControlCodeForValidation => SettingsControlCode;

    internal static uint NightChargeControlCodeForValidation => NightChargeControlCode;

    public ValueTask<HardwareReadResult<ToggleState>> ReadOvernightChargeAsync(
        CancellationToken cancellationToken) =>
        ReadAsync(
            NightChargeControlCode,
            NightChargeReadSelector,
            MapOvernightCharge,
            "overnight_read_timed_out",
            "overnight_read_failed",
            cancellationToken);

    public ValueTask<HardwareReadResult<ToggleState>> ReadFnLockAsync(
        CancellationToken cancellationToken) =>
        ReadAsync(
            SettingsControlCode,
            SettingsReadSelector,
            MapFnLock,
            "fn_lock_read_timed_out",
            "fn_lock_read_failed",
            cancellationToken);

    public ValueTask<HardwareReadResult<AlwaysOnUsbState>> ReadAlwaysOnUsbAsync(
        CancellationToken cancellationToken) =>
        ReadAsync(
            SettingsControlCode,
            SettingsReadSelector,
            MapAlwaysOnUsb,
            "always_on_usb_read_timed_out",
            "always_on_usb_read_failed",
            cancellationToken);

    public ValueTask WriteOvernightChargeAsync(
        ToggleState desired,
        CancellationToken cancellationToken) =>
        WriteAsync(
            NightChargeControlCode,
            [desired == ToggleState.Enabled ? NightChargeOn : NightChargeOff],
            "overnight_write_timed_out",
            "overnight_write_failed",
            "overnight_setter_not_supported",
            cancellationToken);

    public ValueTask WriteFnLockAsync(
        ToggleState desired,
        CancellationToken cancellationToken) =>
        WriteAsync(
            SettingsControlCode,
            [desired == ToggleState.Enabled ? FnLockOn : FnLockOff],
            "fn_lock_write_timed_out",
            "fn_lock_write_failed",
            "fn_lock_setter_not_supported",
            cancellationToken);

    public ValueTask WriteAlwaysOnUsbAsync(
        AlwaysOnUsbState desired,
        CancellationToken cancellationToken) =>
        WriteAsync(
            SettingsControlCode,
            desired switch
            {
                AlwaysOnUsbState.Off => [0xBu, 0x12u],
                AlwaysOnUsbState.OnWhenSleeping => [0xAu, 0x12u],
                AlwaysOnUsbState.OnAlways => [0xAu, 0x13u],
                _ => throw new HardwareWriteException(
                    "always_on_usb_value_invalid",
                    HardwareWriteStatus.Failed),
            },
            "always_on_usb_write_timed_out",
            "always_on_usb_write_failed",
            "always_on_usb_setter_not_supported",
            cancellationToken);

    internal static HardwareReadResult<ToggleState> MapOvernightCharge(uint rawValue)
    {
        if (!GetBit(rawValue, 0))
        {
            return HardwareReadResult<ToggleState>.Failure(
                HardwareReadStatus.Unsupported,
                "overnight_not_supported");
        }

        return HardwareReadResult<ToggleState>.Success(
            GetBit(rawValue, 4) ? ToggleState.Enabled : ToggleState.Disabled);
    }

    internal static HardwareReadResult<ToggleState> MapFnLock(uint rawValue) =>
        HardwareReadResult<ToggleState>.Success(
            GetBit(rawValue, 10) ? ToggleState.Enabled : ToggleState.Disabled);

    internal static HardwareReadResult<AlwaysOnUsbState> MapAlwaysOnUsb(uint rawValue)
    {
        uint reversed = BinaryPrimitives.ReverseEndianness(rawValue);
        if (!GetBit(reversed, 31))
            return HardwareReadResult<AlwaysOnUsbState>.Success(AlwaysOnUsbState.Off);

        return HardwareReadResult<AlwaysOnUsbState>.Success(
            GetBit(reversed, 23) ? AlwaysOnUsbState.OnAlways : AlwaysOnUsbState.OnWhenSleeping);
    }

    private async ValueTask<HardwareReadResult<T>> ReadAsync<T>(
        uint controlCode,
        uint selector,
        Func<uint, HardwareReadResult<T>> map,
        string timedOutCode,
        string failedCode,
        CancellationToken cancellationToken)
        where T : struct
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            uint rawValue = await Task
                .Run(() => Send(controlCode, selector), CancellationToken.None)
                .WaitAsync(IoTimeout, cancellationToken)
                .ConfigureAwait(false);
            return map(rawValue);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return HardwareReadResult<T>.Failure(HardwareReadStatus.TimedOut, timedOutCode);
        }
        catch (Win32Exception exception)
        {
            return MapReadWin32<T>(exception.NativeErrorCode, failedCode);
        }
        catch (Exception)
        {
            return HardwareReadResult<T>.Failure(HardwareReadStatus.Failed, failedCode);
        }
    }

    private async ValueTask WriteAsync(
        uint controlCode,
        uint[] selectors,
        string timedOutCode,
        string failedCode,
        string unsupportedCode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await Task
                .Run(
                    () =>
                    {
                        foreach (uint selector in selectors)
                            _ = Send(controlCode, selector);
                    },
                    CancellationToken.None)
                .WaitAsync(IoTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            throw new HardwareWriteException(timedOutCode, HardwareWriteStatus.Failed);
        }
        catch (HardwareWriteException)
        {
            throw;
        }
        catch (Win32Exception exception)
        {
            throw MapWriteWin32(exception.NativeErrorCode, failedCode, unsupportedCode);
        }
        catch (Exception)
        {
            throw new HardwareWriteException(failedCode, HardwareWriteStatus.Failed);
        }
    }

    private static HardwareReadResult<T> MapReadWin32<T>(int errorCode, string failedCode)
        where T : struct =>
        errorCode switch
        {
            ErrorFileNotFound or ErrorPathNotFound =>
                HardwareReadResult<T>.Failure(
                    HardwareReadStatus.Unavailable,
                    "energy_driver_not_available"),
            ErrorAccessDenied =>
                HardwareReadResult<T>.Failure(
                    HardwareReadStatus.AccessDenied,
                    "energy_driver_access_denied"),
            ErrorInvalidFunction or ErrorNotSupported =>
                HardwareReadResult<T>.Failure(
                    HardwareReadStatus.Unsupported,
                    "energy_feature_not_supported"),
            _ => HardwareReadResult<T>.Failure(HardwareReadStatus.Failed, failedCode),
        };

    private static HardwareWriteException MapWriteWin32(
        int errorCode,
        string failedCode,
        string unsupportedCode) =>
        errorCode switch
        {
            ErrorFileNotFound or ErrorPathNotFound =>
                new("energy_driver_not_available", HardwareWriteStatus.Failed),
            ErrorAccessDenied =>
                new("energy_driver_access_denied", HardwareWriteStatus.Failed),
            ErrorInvalidFunction or ErrorNotSupported =>
                new(unsupportedCode, HardwareWriteStatus.Unsupported),
            _ => new(failedCode, HardwareWriteStatus.Failed),
        };

    private static uint Send(uint controlCode, uint selector)
    {
        using SafeFileHandle handle = CreateFile(
            DevicePath,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            nint.Zero,
            OpenExisting,
            FileAttributeNormal,
            nint.Zero);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        if (!DeviceIoControl(
                handle,
                controlCode,
                in selector,
                sizeof(uint),
                out uint output,
                sizeof(uint),
                out uint bytesReturned,
                nint.Zero))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        if (bytesReturned != sizeof(uint))
            throw new InvalidDataException("EnergyDrv returned an invalid feature payload size.");

        return output;
    }

    private static bool GetBit(uint value, int bit) => ((value >> bit) & 1u) != 0;

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
