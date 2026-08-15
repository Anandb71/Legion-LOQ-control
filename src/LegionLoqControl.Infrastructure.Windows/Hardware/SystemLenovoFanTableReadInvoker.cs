using System.Management;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal sealed class SystemLenovoFanTableReadInvoker : IFanTableReader
{
    internal const string MethodName = "Fan_Get_Table";

    public async ValueTask<HardwareReadResult<FanTableSnapshot>> ReadAsync(
        CancellationToken cancellationToken)
    {
        WindowsPlatform.EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await Task
                .Run(ReadCore, CancellationToken.None)
                .WaitAsync(LenovoWmiScope.Timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.TimedOut,
                "wmi_getter_timed_out");
        }
        catch (LenovoWmiReadFailureException exception)
        {
            return HardwareReadResult<FanTableSnapshot>.Failure(
                exception.Status,
                exception.ErrorCode);
        }
        catch (LenovoWmiNoInstanceException)
        {
            return HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.Unavailable,
                "wmi_provider_object_not_found");
        }
        catch (InvalidDataException)
        {
            return HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.InvalidData,
                "wmi_output_invalid");
        }
        catch (Exception)
        {
            return HardwareReadResult<FanTableSnapshot>.Failure(
                HardwareReadStatus.Failed,
                "wmi_getter_failed");
        }
    }

    private static HardwareReadResult<FanTableSnapshot> ReadCore()
    {
        try
        {
            using ManagementObject instance = LenovoWmiScope.GetInstance(LenovoWmiScope.FanMethodClass);
            using ManagementBaseObject output = LenovoWmiScope.Invoke(
                instance,
                MethodName,
                input =>
                {
                    input["FanID"] = (byte)0;
                    input["SensorID"] = (byte)0;
                });

            uint[] fanTable = LenovoWmiScope.ToUInt32Array(output.Properties["FanTable"]?.Value);
            uint[] sensorTable = LenovoWmiScope.ToUInt32Array(output.Properties["SensorTable"]?.Value);
            if (TryReadUInt32(output, "FanTableSize", out uint expectedFan) &&
                expectedFan != fanTable.Length)
            {
                throw new InvalidDataException("Fan table size does not match.");
            }

            if (TryReadUInt32(output, "SensorTableSize", out uint expectedSensor) &&
                expectedSensor != sensorTable.Length)
            {
                throw new InvalidDataException("Sensor table size does not match.");
            }

            return FanTableParser.Parse(0, 0, fanTable, sensorTable);
        }
        catch (ManagementException exception)
        {
            throw LenovoWmiScope.Map(
                exception,
                "wmi_getter_not_available",
                "wmi_getter_timed_out",
                "wmi_getter_failed");
        }
    }

    private static bool TryReadUInt32(
        ManagementBaseObject output,
        string name,
        out uint value)
    {
        try
        {
            if (output.Properties[name]?.Value is uint number)
            {
                value = number;
                return true;
            }
        }
        catch (ManagementException)
        {
        }

        value = 0;
        return false;
    }
}
