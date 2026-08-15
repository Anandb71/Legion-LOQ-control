using Microsoft.Management.Infrastructure;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal sealed class SystemLenovoWmiWriteInvoker : ILenovoWmiWriteInvoker
{
    public async ValueTask WriteAsync(
        LenovoWmiWriteOperation operation,
        uint data,
        CancellationToken cancellationToken)
    {
        WindowsPlatform.EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation));

        try
        {
            await Task
                .Run(() => WriteCore(operation, data), CancellationToken.None)
                .WaitAsync(LenovoCimScope.Timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new LenovoWmiReadFailureException(
                HardwareReadStatus.TimedOut,
                "wmi_setter_timed_out",
                exception);
        }
    }

    internal static string MethodName(LenovoWmiWriteOperation operation) =>
        operation switch
        {
            LenovoWmiWriteOperation.ThermalMode => "SetSmartFanMode",
            LenovoWmiWriteOperation.DisplayOverdrive => "SetODStatus",
            LenovoWmiWriteOperation.IntegratedGpuMode => "SetIGPUModeStatus",
            LenovoWmiWriteOperation.LightControlOwner => "SetLightControlOwner",
            LenovoWmiWriteOperation.TouchpadLock => "SetTPStatus",
            LenovoWmiWriteOperation.WinKeyLock => "SetWinKeyStatus",
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

    private static void WriteCore(LenovoWmiWriteOperation operation, uint data)
    {
        string methodName = MethodName(operation);
        try
        {
            using CimSession session = LenovoCimScope.CreateSession();
            using CimInstance instance = LenovoCimScope.GetInstance(
                session,
                LenovoCimScope.GameZoneClass);
            using var parameters = new CimMethodParametersCollection
            {
                CimMethodParameter.Create("Data", data, CimFlags.In),
            };
            using CimMethodResult output = LenovoCimScope.Invoke(
                session,
                instance,
                methodName,
                parameters);
            SystemLenovoWmiReadInvoker.ValidateReturnValue(
                LenovoCimScope.GetParameter(output, "ReturnValue"));
        }
        catch (LenovoWmiNoInstanceException)
        {
            throw new LenovoWmiReadFailureException(
                HardwareReadStatus.Unavailable,
                "wmi_provider_object_not_found");
        }
        catch (CimException exception)
        {
            throw LenovoCimScope.Map(
                exception,
                "wmi_setter_not_available",
                "wmi_setter_timed_out",
                "wmi_setter_failed");
        }
    }
}
