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

    public WindowsHardwareStateReader()
        : this(new SystemLenovoWmiReadInvoker())
    {
    }

    internal WindowsHardwareStateReader(ILenovoWmiReadInvoker invoker)
    {
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
    }

    public ValueTask<HardwareReadResult<BatteryChargeMode>> ReadBatteryChargeModeAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
        catch (ManagementException exception) when (exception.ErrorCode == ManagementStatus.Timedout)
        {
            return HardwareReadResult<T>.Failure(HardwareReadStatus.TimedOut, "wmi_getter_timed_out");
        }
        catch (ManagementException exception) when (
            exception.ErrorCode is ManagementStatus.InvalidClass or
                ManagementStatus.InvalidMethod or
                ManagementStatus.NotFound)
        {
            return HardwareReadResult<T>.Failure(HardwareReadStatus.Unsupported, "wmi_getter_not_available");
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
