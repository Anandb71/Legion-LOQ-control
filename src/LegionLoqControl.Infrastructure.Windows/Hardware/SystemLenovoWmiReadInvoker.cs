using System.Management;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal enum LenovoWmiReadOperation
{
    ThermalMode = 0,
    DisplayOverdrive = 1,
    IntegratedGpuMode = 2,
}

internal interface ILenovoWmiReadInvoker
{
    ValueTask<uint> ReadAsync(
        LenovoWmiReadOperation operation,
        CancellationToken cancellationToken);
}

internal sealed class SystemLenovoWmiReadInvoker : ILenovoWmiReadInvoker
{
    private const string OutputProperty = "Data";

    public async ValueTask<uint> ReadAsync(
        LenovoWmiReadOperation operation,
        CancellationToken cancellationToken)
    {
        WindowsPlatform.EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await Task
                .Run(() => ReadCore(operation), CancellationToken.None)
                .WaitAsync(LenovoWmiScope.Timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new LenovoWmiReadFailureException(
                HardwareReadStatus.TimedOut,
                "wmi_getter_timed_out",
                exception);
        }
    }

    internal static void ValidateReturnValue(object? value)
    {
        switch (value)
        {
            case true:
            case 1:
            case 1u:
                return;
            case false:
            case 0:
            case 0u:
                throw new LenovoWmiMethodRejectedException();
            default:
                throw new InvalidDataException("Lenovo WMI getter returned a non-Boolean status.");
        }
    }

    internal static uint ConvertDataToUInt32(object? value) =>
        value switch
        {
            uint data => data,
            int data when data >= 0 => (uint)data,
            ushort data => data,
            byte data => data,
            _ => throw new InvalidDataException("Lenovo WMI getter returned a non-UInt32 value."),
        };

    private static uint ReadCore(LenovoWmiReadOperation operation)
    {
        string methodName = operation switch
        {
            LenovoWmiReadOperation.ThermalMode => "GetSmartFanMode",
            LenovoWmiReadOperation.DisplayOverdrive => "GetODStatus",
            LenovoWmiReadOperation.IntegratedGpuMode => "GetIGPUModeStatus",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

        using ManagementObject instance = LenovoWmiScope.GetInstance(LenovoWmiScope.GameZoneClass);
        using ManagementBaseObject output = LenovoWmiScope.Invoke(instance, methodName);
        ValidateReturnValue(output.Properties["ReturnValue"]?.Value);
        return ConvertDataToUInt32(output.Properties[OutputProperty]?.Value);
    }
}

internal sealed class LenovoWmiNoInstanceException : Exception;

internal sealed class LenovoWmiMethodRejectedException : Exception;
