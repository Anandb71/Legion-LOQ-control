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
    private readonly EnergyDriverFeatureClient? _energyFeatures;
    private readonly IFourZoneKeyboardHid? _keyboard;
    private readonly IFanTableReader? _fanTable;
    private readonly ISpectrumKeyboardHid? _spectrum;
    private readonly OemFanTableStore _oemFanTable;

    public WindowsHardwareStateReader()
        : this(new SystemLenovoWmiReadInvoker(), batteryReader: null, keyboard: null, fanTable: null, energyFeatures: null, spectrum: null)
    {
    }

    internal WindowsHardwareStateReader(ILenovoWmiReadInvoker invoker)
        : this(invoker, batteryReader: null, keyboard: null, fanTable: null, energyFeatures: null, spectrum: null)
    {
    }

    internal WindowsHardwareStateReader(
        ILenovoWmiReadInvoker invoker,
        IEnergyDriverBatteryReader? batteryReader)
        : this(invoker, batteryReader, keyboard: null, fanTable: null, energyFeatures: null, spectrum: null)
    {
    }

    internal WindowsHardwareStateReader(
        ILenovoWmiReadInvoker invoker,
        IEnergyDriverBatteryReader? batteryReader,
        IFourZoneKeyboardHid? keyboard)
        : this(invoker, batteryReader, keyboard, fanTable: null, energyFeatures: null, spectrum: null)
    {
    }

    internal WindowsHardwareStateReader(
        ILenovoWmiReadInvoker invoker,
        IEnergyDriverBatteryReader? batteryReader,
        IFourZoneKeyboardHid? keyboard,
        IFanTableReader? fanTable,
        EnergyDriverFeatureClient? energyFeatures = null,
        ISpectrumKeyboardHid? spectrum = null,
        OemFanTableStore? oemFanTable = null)
    {
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        _batteryReader = batteryReader;
        _keyboard = keyboard;
        _fanTable = fanTable;
        _energyFeatures = energyFeatures;
        _spectrum = spectrum;
        _oemFanTable = oemFanTable ?? new OemFanTableStore();
    }

    internal static WindowsHardwareStateReader CreatePrivilegedReadOnly() =>
        new(
            new SystemLenovoWmiReadInvoker(),
            new EnergyDriverBatteryReader(),
            new FourZoneKeyboardHid(),
            new SystemLenovoFanTableReadInvoker(),
            new EnergyDriverFeatureClient(),
            new SpectrumKeyboardHid());

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

    public async ValueTask<HardwareReadResult<FanTableSnapshot>> ReadFanTableAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_fanTable is null)
        {
            return HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.Unavailable,
                "fan_table_not_opened");
        }

        HardwareReadResult<FanTableSnapshot> result = await _fanTable
            .ReadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (result.Status == HardwareReadStatus.Success && result.Value.HasValue)
            _oemFanTable.SaveIfAbsent(result.Value.Value);

        return result;
    }

    public ValueTask<HardwareReadResult<ToggleState>> ReadOvernightChargeAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _energyFeatures is null
            ? ValueTask.FromResult(HardwareReadResult<ToggleState>.Failure(
                HardwareReadStatus.Unavailable,
                "overnight_not_opened"))
            : _energyFeatures.ReadOvernightChargeAsync(cancellationToken);
    }

    public ValueTask<HardwareReadResult<ToggleState>> ReadFnLockAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _energyFeatures is null
            ? ValueTask.FromResult(HardwareReadResult<ToggleState>.Failure(
                HardwareReadStatus.Unavailable,
                "fn_lock_not_opened"))
            : _energyFeatures.ReadFnLockAsync(cancellationToken);
    }

    public ValueTask<HardwareReadResult<AlwaysOnUsbState>> ReadAlwaysOnUsbAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _energyFeatures is null
            ? ValueTask.FromResult(HardwareReadResult<AlwaysOnUsbState>.Failure(
                HardwareReadStatus.Unavailable,
                "always_on_usb_not_opened"))
            : _energyFeatures.ReadAlwaysOnUsbAsync(cancellationToken);
    }

    public ValueTask<HardwareReadResult<ToggleState>> ReadTouchpadLockAsync(
        CancellationToken cancellationToken) =>
        ReadSupportedToggleAsync(
            LenovoWmiReadOperation.TouchpadLockSupport,
            LenovoWmiReadOperation.TouchpadLock,
            "touchpad_not_supported",
            "unexpected_touchpad_value",
            cancellationToken);

    public ValueTask<HardwareReadResult<ToggleState>> ReadWinKeyLockAsync(
        CancellationToken cancellationToken) =>
        ReadSupportedToggleAsync(
            LenovoWmiReadOperation.WinKeyLockSupport,
            LenovoWmiReadOperation.WinKeyLock,
            "win_key_not_supported",
            "unexpected_win_key_value",
            cancellationToken);

    public ValueTask<HardwareReadResult<FourZoneLightingState>> ReadFourZoneLightingAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_keyboard is not null)
            return _keyboard.ReadLightingAsync(cancellationToken);

        return ValueTask.FromResult(HardwareReadResult<FourZoneLightingState>.Failure(
            HardwareReadStatus.Unavailable,
            "keyboard_hid_not_opened"));
    }

    public ValueTask<HardwareReadResult<SpectrumBrightness>> ReadSpectrumKeyboardAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_spectrum is not null)
            return _spectrum.ReadAsync(cancellationToken);

        return ValueTask.FromResult(HardwareReadResult<SpectrumBrightness>.Failure(
            HardwareReadStatus.Unavailable,
            "spectrum_hid_not_opened"));
    }

    private async ValueTask<HardwareReadResult<ToggleState>> ReadSupportedToggleAsync(
        LenovoWmiReadOperation supportOperation,
        LenovoWmiReadOperation valueOperation,
        string unsupportedCode,
        string invalidValueCode,
        CancellationToken cancellationToken)
    {
        HardwareReadResult<uint> support = await ReadRawAsync(supportOperation, cancellationToken)
            .ConfigureAwait(false);
        if (support.Status != HardwareReadStatus.Success ||
            !support.Value.HasValue ||
            support.Value.Value == 0)
        {
            return HardwareReadResult<ToggleState>.Failure(
                support.Status == HardwareReadStatus.Success
                    ? HardwareReadStatus.Unsupported
                    : support.Status,
                support.Status == HardwareReadStatus.Success
                    ? unsupportedCode
                    : support.ErrorCode ?? unsupportedCode);
        }

        return await ReadAsync<ToggleState>(
                valueOperation,
                static raw => raw switch
                {
                    0 => ToggleState.Disabled,
                    1 => ToggleState.Enabled,
                    _ => (ToggleState?)null,
                },
                invalidValueCode,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<HardwareReadResult<uint>> ReadRawAsync(
        LenovoWmiReadOperation operation,
        CancellationToken cancellationToken)
    {
        try
        {
            uint rawValue = await _invoker
                .ReadAsync(operation, cancellationToken)
                .ConfigureAwait(false);
            return HardwareReadResult<uint>.Success(rawValue);
        }
        catch (LenovoWmiReadFailureException exception)
        {
            return HardwareReadResult<uint>.Failure(exception.Status, exception.ErrorCode);
        }
        catch (Exception)
        {
            return HardwareReadResult<uint>.Failure(
                HardwareReadStatus.Failed,
                "wmi_getter_failed");
        }
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
