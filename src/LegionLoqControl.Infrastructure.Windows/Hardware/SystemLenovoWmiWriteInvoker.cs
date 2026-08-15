using System.Management;
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

        await Task
            .Run(() => WriteCore(operation, data), CancellationToken.None)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    internal static string MethodName(LenovoWmiWriteOperation operation) =>
        operation switch
        {
            LenovoWmiWriteOperation.ThermalMode => "SetSmartFanMode",
            LenovoWmiWriteOperation.DisplayOverdrive => "SetODStatus",
            LenovoWmiWriteOperation.IntegratedGpuMode => "SetIGPUModeStatus",
            LenovoWmiWriteOperation.LightControlOwner => "SetLightControlOwner",
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

    private static void WriteCore(LenovoWmiWriteOperation operation, uint data)
    {
        string methodName = MethodName(operation);
        try
        {
            using ManagementObject instance = LenovoWmiScope.GetInstance(LenovoWmiScope.GameZoneClass);
            using ManagementBaseObject? input = instance.GetMethodParameters(methodName);
            if (input is null)
                throw new InvalidDataException("Lenovo WMI setter has no input object.");

            input["Data"] = data;
            using ManagementBaseObject? output = instance.InvokeMethod(
                methodName,
                input,
                LenovoWmiScope.MethodOptions());
            if (output is null)
                throw new InvalidDataException("Lenovo WMI setter returned no output object.");

            SystemLenovoWmiReadInvoker.ValidateReturnValue(output.Properties["ReturnValue"]?.Value);
        }
        catch (LenovoWmiNoInstanceException)
        {
            throw new LenovoWmiReadFailureException(
                HardwareReadStatus.Unavailable,
                "wmi_provider_object_not_found");
        }
        catch (ManagementException exception)
        {
            throw LenovoWmiScope.Map(
                exception,
                "wmi_setter_not_available",
                "wmi_setter_timed_out",
                "wmi_setter_failed");
        }
    }
}
