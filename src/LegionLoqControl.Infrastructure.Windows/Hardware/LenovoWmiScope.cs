using System.Management;
using LegionLoqControl.Domain.Results;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal static class LenovoWmiScope
{
    internal const string Path = @"root\WMI";
    internal const string GameZoneClass = "LENOVO_GAMEZONE_DATA";
    internal const string FanMethodClass = "LENOVO_FAN_METHOD";
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    internal static ManagementObject GetInstance(string className)
    {
        if (className is not (GameZoneClass or FanMethodClass))
            throw new ArgumentOutOfRangeException(nameof(className));

        ManagementScope scope = Connect();
        ManagementObject? selected = FindInstance(scope, new SelectQuery(className));
        if (selected is null)
        {
            using var managementClass = new ManagementClass(scope, new ManagementPath(className), null);
            selected = FindInstance(managementClass.GetInstances());
        }

        return selected ?? throw new LenovoWmiNoInstanceException();
    }

    internal static ManagementBaseObject Invoke(
        ManagementObject instance,
        string methodName,
        Action<ManagementBaseObject>? bindInput = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        using var managementClass = new ManagementClass(
            instance.Scope,
            new ManagementPath(instance.ClassPath.ClassName),
            null);
        using ManagementBaseObject? input = managementClass.GetMethodParameters(methodName);
        if (bindInput is not null)
        {
            if (input is null)
                throw new InvalidDataException("Lenovo WMI method has no input object.");
            bindInput(input);
        }

        return instance.InvokeMethod(methodName, input, MethodOptions())
            ?? throw new InvalidDataException("Lenovo WMI method returned no output object.");
    }

    internal static InvokeMethodOptions MethodOptions() =>
        new() { Timeout = Timeout };

    internal static uint[] ToUInt32Array(object? value)
    {
        if (value is uint[] uints)
            return uints;
        if (value is not Array array)
            throw new InvalidDataException("Lenovo WMI returned a non-array table.");

        var result = new uint[array.Length];
        for (int index = 0; index < array.Length; index++)
        {
            result[index] = array.GetValue(index) switch
            {
                uint number => number,
                int number when number >= 0 => (uint)number,
                ushort number => number,
                byte number => number,
                _ => throw new InvalidDataException("Lenovo WMI returned a non-UInt32 table value."),
            };
        }

        return result;
    }

    internal static LenovoWmiReadFailureException Map(
        ManagementException exception,
        string unsupportedCode,
        string timedOutCode,
        string failedCode)
    {
        return exception.ErrorCode switch
        {
            ManagementStatus.AccessDenied => new(
                HardwareReadStatus.AccessDenied,
                "wmi_access_denied"),
            ManagementStatus.NotFound => new(
                HardwareReadStatus.Unavailable,
                "wmi_provider_object_not_found"),
            ManagementStatus.InvalidClass or ManagementStatus.InvalidMethod => new(
                HardwareReadStatus.Unsupported,
                unsupportedCode),
            ManagementStatus.Timedout => new(
                HardwareReadStatus.TimedOut,
                timedOutCode),
            _ => new(HardwareReadStatus.Failed, failedCode),
        };
    }

    private static ManagementScope Connect()
    {
        var scope = new ManagementScope(@"\\.\root\WMI");
        scope.Options.EnablePrivileges = true;
        scope.Options.Impersonation = ImpersonationLevel.Impersonate;
        scope.Options.Timeout = Timeout;
        scope.Connect();
        return scope;
    }

    private static ManagementObject? FindInstance(ManagementScope scope, SelectQuery query)
    {
        using var searcher = new ManagementObjectSearcher(scope, query);
        searcher.Options.Timeout = Timeout;
        return FindInstance(searcher.Get());
    }

    private static ManagementObject? FindInstance(ManagementObjectCollection instances)
    {
        using (instances)
        {
            ManagementObject[] items = instances.Cast<ManagementObject>().ToArray();
            ManagementObject? selected =
                items.FirstOrDefault(IsActive) ?? items.FirstOrDefault();
            foreach (ManagementObject item in items)
            {
                if (!ReferenceEquals(item, selected))
                    item.Dispose();
            }

            return selected;
        }
    }

    private static bool IsActive(ManagementObject instance)
    {
        try
        {
            return instance.Properties["Active"]?.Value is true or 1 or 1u;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
