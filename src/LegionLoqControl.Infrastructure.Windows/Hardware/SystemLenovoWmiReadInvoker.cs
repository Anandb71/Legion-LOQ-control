using System.Management;
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
    private const string ScopePath = @"root\WMI";
    private const string ClassName = "LENOVO_GAMEZONE_DATA";
    private const string OutputProperty = "Data";
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(5);

    public async ValueTask<uint> ReadAsync(
        LenovoWmiReadOperation operation,
        CancellationToken cancellationToken)
    {
        WindowsPlatform.EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();

        return await Task
            .Run(() => ReadCore(operation), CancellationToken.None)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    internal static void ValidateReturnValue(object? value)
    {
        if (value is not bool succeeded)
            throw new InvalidDataException("Lenovo WMI getter returned a non-Boolean status.");
        if (!succeeded)
            throw new LenovoWmiMethodRejectedException();
    }

    internal static uint ConvertDataToUInt32(object? value)
    {
        if (value is not uint data)
            throw new InvalidDataException("Lenovo WMI getter returned a non-UInt32 value.");

        return data;
    }

    private static uint ReadCore(LenovoWmiReadOperation operation)
    {
        string methodName = operation switch
        {
            LenovoWmiReadOperation.ThermalMode => "GetSmartFanMode",
            LenovoWmiReadOperation.DisplayOverdrive => "GetODStatus",
            LenovoWmiReadOperation.IntegratedGpuMode => "GetIGPUModeStatus",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

        using var searcher = new ManagementObjectSearcher(
            ScopePath,
            $"SELECT * FROM {ClassName}");
        searcher.Options.Timeout = OperationTimeout;
        using ManagementObjectCollection instances = searcher.Get();
        using ManagementObject? instance = instances
            .Cast<ManagementObject>()
            .FirstOrDefault();
        if (instance is null)
            throw new LenovoWmiNoInstanceException();

        var options = new InvokeMethodOptions { Timeout = OperationTimeout };
        using ManagementBaseObject? output = instance.InvokeMethod(methodName, null, options);
        if (output is null)
            throw new InvalidDataException("Lenovo WMI getter returned no output object.");

        ValidateReturnValue(output.Properties["ReturnValue"]?.Value);
        return ConvertDataToUInt32(output.Properties[OutputProperty]?.Value);
    }
}

internal sealed class LenovoWmiNoInstanceException : Exception;

internal sealed class LenovoWmiMethodRejectedException : Exception;
