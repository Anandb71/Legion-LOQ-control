using Microsoft.Management.Infrastructure;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal enum LenovoWmiReadOperation
{
    ThermalMode = 0,
    DisplayOverdrive = 1,
    IntegratedGpuMode = 2,
    TouchpadLock = 3,
    WinKeyLock = 4,
    TouchpadLockSupport = 5,
    WinKeyLockSupport = 6,
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
                .WaitAsync(LenovoCimScope.Timeout, cancellationToken)
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
            LenovoWmiReadOperation.TouchpadLock => "GetTPStatus",
            LenovoWmiReadOperation.WinKeyLock => "GetWinKeyStatus",
            LenovoWmiReadOperation.TouchpadLockSupport => "IsSupportDisableTP",
            LenovoWmiReadOperation.WinKeyLockSupport => "IsSupportDisableWinKey",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

        try
        {
            using CimSession session = LenovoCimScope.CreateSession();
            using CimInstance instance = LenovoCimScope.GetInstance(
                session,
                LenovoCimScope.GameZoneClass);
            using CimMethodResult output = LenovoCimScope.Invoke(session, instance, methodName);
            ValidateReturnValue(LenovoCimScope.GetParameter(output, "ReturnValue"));
            return ConvertDataToUInt32(LenovoCimScope.GetParameter(output, OutputProperty));
        }
        catch (CimException exception)
        {
            throw LenovoCimScope.Map(
                exception,
                "wmi_getter_not_available",
                "wmi_getter_timed_out",
                "wmi_getter_failed");
        }
    }
}

internal sealed class LenovoWmiNoInstanceException : Exception;

internal sealed class LenovoWmiMethodRejectedException : Exception;
