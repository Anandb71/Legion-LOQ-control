using System.Management;
using System.Runtime.InteropServices;
using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

public sealed class WindowsHardwareStateReader : IHardwareStateReader
{
    private const int HResultAccessDenied = unchecked((int)0x80070005);
    private const int WbemAccessDenied = unchecked((int)0x80041003);

    private readonly ILenovoWmiReadInvoker _invoker;
    private readonly IEnergyDriverBatteryReader? _batteryReader;
    private readonly IFourZoneKeyboardHid? _keyboard;
    private readonly IFanTableReader? _fanTable;

    public WindowsHardwareStateReader()
        : this(new SystemLenovoWmiReadInvoker(), batteryReader: null, keyboard: null, fanTable: null)
    {
    }

    internal WindowsHardwareStateReader(ILenovoWmiReadInvoker invoker)
        : this(invoker, batteryReader: null, keyboard: null, fanTable: null)
    {
    }

    internal WindowsHardwareStateReader(
        ILenovoWmiReadInvoker invoker,
        IEnergyDriverBatteryReader? batteryReader)
        : this(invoker, batteryReader, keyboard: null, fanTable: null)
    {
    }

    internal WindowsHardwareStateReader(
        ILenovoWmiReadInvoker invoker,
        IEnergyDriverBatteryReader? batteryReader,
        IFourZoneKeyboardHid? keyboard)
        : this(invoker, batteryReader, keyboard, fanTable: null)
    {
    }

    internal WindowsHardwareStateReader(
        ILenovoWmiReadInvoker invoker,
        IEnergyDriverBatteryReader? batteryReader,
        IFourZoneKeyboardHid? keyboard,
        IFanTableReader? fanTable)
    {
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        _batteryReader = batteryReader;
        _keyboard = keyboard;
        _fanTable = fanTable;
    }

    internal static WindowsHardwareStateReader CreatePrivilegedReadOnly() =>
        new(
            new SystemLenovoWmiReadInvoker(),
            new EnergyDriverBatteryReader(),
            new FourZoneKeyboardHid(),
            new SystemLenovoFanTableReadInvoker());

    public ValueTask<HardwareReadResult<BatteryChargeMode>> ReadBatteryChargeModeAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_batteryReader is not null)
            return _batteryReader.ReadAsync(cancellationToken);

        return ValueTask.FromResult(HardwareReadResult<BatteryChargeMode>.Failure(
            HardwareReadStatus.Unavailable,
            "battery_read_transport_not_implemented"));
    }

    public ValueTask<HardwareReadResult<ThermalMode>> ReadThermalModeAsync(
        CancellationToken cancellationToken) =>
        ReadAsync<ThermalMode>(
            LenovoWmiReadOperation.ThermalMode,
            static raw => raw switch
            {
                1 => ThermalMode.Quiet,
                2 => ThermalMode.Balanced,
                3 => ThermalMode.Performance,
                224 => ThermalMode.Extreme,
                255 => ThermalMode.Custom,
                _ => (ThermalMode?)null,
            },
            "unexpected_thermal_mode_value",
            cancellationToken);

    public ValueTask<HardwareReadResult<ToggleState>> ReadDisplayOverdriveAsync(
        CancellationToken cancellationToken) =>
        ReadAsync<ToggleState>(
            LenovoWmiReadOperation.DisplayOverdrive,
            static raw => raw switch
            {
                0 => ToggleState.Disabled,
                1 => ToggleState.Enabled,
                _ => (ToggleState?)null,
            },
            "unexpected_overdrive_value",
            cancellationToken);

    public ValueTask<HardwareReadResult<IntegratedGpuMode>> ReadIntegratedGpuModeAsync(
        CancellationToken cancellationToken) =>
        ReadAsync<IntegratedGpuMode>(
            LenovoWmiReadOperation.IntegratedGpuMode,
            static raw => raw switch
            {
                0 => IntegratedGpuMode.Default,
                1 => IntegratedGpuMode.IntegratedOnly,
                2 => IntegratedGpuMode.Automatic,
                _ => (IntegratedGpuMode?)null,
            },
            "unexpected_integrated_gpu_mode_value",
            cancellationToken);

    public ValueTask<HardwareReadResult<FourZoneKeyboardMode>> ReadFourZoneKeyboardAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_keyboard is not null)
            return _keyboard.ReadAsync(cancellationToken);

        return ValueTask.FromResult(HardwareReadResult<FourZoneKeyboardMode>.Failure(
            HardwareReadStatus.Unavailable,
            "keyboard_hid_not_opened"));
    }

    public ValueTask<HardwareReadResult<FanTableSnapshot>> ReadFanTableAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_fanTable is not null)
            return _fanTable.ReadAsync(cancellationToken);

        return ValueTask.FromResult(HardwareReadResult<FanTableSnapshot>.Failure(
            HardwareReadStatus.Unavailable,
            "fan_table_not_opened"));
    }

    private async ValueTask<HardwareReadResult<T>> ReadAsync<T>(
        LenovoWmiReadOperation operation,
        Func<uint, T?> map,
        string invalidValueCode,
        CancellationToken cancellationToken)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(map);
        WindowsPlatform.EnsureSupported();

        try
        {
            uint rawValue = await _invoker
                .ReadAsync(operation, cancellationToken)
                .ConfigureAwait(false);
            T? value = map(rawValue);
            return value.HasValue
                ? HardwareReadResult<T>.Success(value.Value)
                : HardwareReadResult<T>.Failure(HardwareReadStatus.InvalidData, invalidValueCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (LenovoWmiReadFailureException exception)
        {
            return HardwareReadResult<T>.Failure(exception.Status, exception.ErrorCode);
        }
        catch (UnauthorizedAccessException)
        {
            return HardwareReadResult<T>.Failure(HardwareReadStatus.AccessDenied, "wmi_access_denied");
        }
        catch (ManagementException exception) when (exception.ErrorCode == ManagementStatus.AccessDenied)
        {
            return HardwareReadResult<T>.Failure(HardwareReadStatus.AccessDenied, "wmi_access_denied");
        }
        catch (COMException exception) when (
            exception.HResult is HResultAccessDenied or WbemAccessDenied)
        {
            return HardwareReadResult<T>.Failure(HardwareReadStatus.AccessDenied, "wmi_access_denied");
        }
        catch (TimeoutException)
        {
            return HardwareReadResult<T>.Failure(HardwareReadStatus.TimedOut, "wmi_getter_timed_out");
        }
        catch (ManagementException exception) when (exception.ErrorCode == ManagementStatus.Timedout)
        {
            return HardwareReadResult<T>.Failure(HardwareReadStatus.TimedOut, "wmi_getter_timed_out");
        }
        catch (ManagementException exception) when (
            exception.ErrorCode == ManagementStatus.InvalidClass)
        {
            return HardwareReadResult<T>.Failure(
                HardwareReadStatus.Unsupported,
                "wmi_class_not_available");
        }
        catch (ManagementException exception) when (
            exception.ErrorCode == ManagementStatus.InvalidMethod)
        {
            return HardwareReadResult<T>.Failure(
                HardwareReadStatus.Unsupported,
                "wmi_getter_not_available");
        }
        catch (ManagementException exception) when (
            exception.ErrorCode == ManagementStatus.NotFound)
        {
            return HardwareReadResult<T>.Failure(
                HardwareReadStatus.Unavailable,
                "wmi_provider_object_not_found");
        }
        catch (LenovoWmiNoInstanceException)
        {
            return HardwareReadResult<T>.Failure(HardwareReadStatus.Unsupported, "wmi_instance_not_found");
        }
        catch (InvalidDataException)
        {
            return HardwareReadResult<T>.Failure(HardwareReadStatus.InvalidData, "wmi_output_invalid");
        }
        catch (LenovoWmiMethodRejectedException)
        {
            return HardwareReadResult<T>.Failure(HardwareReadStatus.Failed, "wmi_getter_rejected");
        }
        catch (Exception)
        {
            return HardwareReadResult<T>.Failure(HardwareReadStatus.Failed, "wmi_getter_failed");
        }
    }

}
