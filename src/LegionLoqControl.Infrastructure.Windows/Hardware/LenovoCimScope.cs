using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal static class LenovoCimScope
{
    internal const string NamespacePath = @"root/WMI";
    internal const string GameZoneClass = "LENOVO_GAMEZONE_DATA";
    internal const string FanMethodClass = "LENOVO_FAN_METHOD";
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    internal static CimSession CreateSession() => CimSession.Create(computerName: null);

    internal static CimInstance GetInstance(CimSession session, string className)
    {
        if (className is not (GameZoneClass or FanMethodClass))
            throw new ArgumentOutOfRangeException(nameof(className));
        ArgumentNullException.ThrowIfNull(session);

        using var options = CreateOperationOptions();
        CimInstance? selected = null;
        try
        {
            foreach (CimInstance instance in session.EnumerateInstances(
                         NamespacePath,
                         className,
                         options))
            {
                if (selected is null || IsActive(instance))
                {
                    selected?.Dispose();
                    selected = instance;
                }
                else
                {
                    instance.Dispose();
                }
            }
        }
        catch
        {
            selected?.Dispose();
            throw;
        }

        return selected ?? throw new LenovoWmiNoInstanceException();
    }

    internal static CimMethodResult Invoke(
        CimSession session,
        CimInstance instance,
        string methodName,
        CimMethodParametersCollection? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        using var options = CreateOperationOptions();
        return session.InvokeMethod(NamespacePath, instance, methodName, parameters, options)
            ?? throw new InvalidDataException("Lenovo CIM method returned no output object.");
    }

    internal static object? GetParameter(CimMethodResult result, string name)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        try
        {
            return result.OutParameters?[name]?.Value;
        }
        catch (CimException)
        {
            return null;
        }
    }

    internal static LenovoWmiReadFailureException Map(
        CimException exception,
        string unsupportedCode,
        string timedOutCode,
        string failedCode)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception.HResult is -2147024891 or -2147217405)
        {
            return new LenovoWmiReadFailureException(
                HardwareReadStatus.AccessDenied,
                "wmi_access_denied");
        }

        return MapNative(
            (int)exception.NativeErrorCode,
            unsupportedCode,
            timedOutCode,
            failedCode);
    }

    internal static LenovoWmiReadFailureException MapNative(
        int nativeErrorCode,
        string unsupportedCode,
        string timedOutCode,
        string failedCode) =>
        nativeErrorCode switch
        {
            2 => new(HardwareReadStatus.AccessDenied, "wmi_access_denied"),
            6 => new(HardwareReadStatus.Unavailable, "wmi_provider_object_not_found"),
            3 or 5 or 7 or 16 or 17 => new(HardwareReadStatus.Unsupported, unsupportedCode),
            20 => new(HardwareReadStatus.TimedOut, timedOutCode),
            _ => new(HardwareReadStatus.Failed, failedCode),
        };

    private static CimOperationOptions CreateOperationOptions() =>
        new() { Timeout = Timeout };

    private static bool IsActive(CimInstance instance)
    {
        try
        {
            return instance.CimInstanceProperties["Active"]?.Value is true or 1 or 1u;
        }
        catch (CimException)
        {
            return false;
        }
    }
}
