using Microsoft.Management.Infrastructure;
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
                .WaitAsync(LenovoCimScope.Timeout, cancellationToken)
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
            using CimSession session = LenovoCimScope.CreateSession();
            using CimInstance instance = LenovoCimScope.GetInstance(
                session,
                LenovoCimScope.FanMethodClass);
            using var parameters = new CimMethodParametersCollection
            {
                CimMethodParameter.Create("FanID", (byte)0, CimFlags.In),
                CimMethodParameter.Create("SensorID", (byte)0, CimFlags.In),
            };
            using CimMethodResult output = LenovoCimScope.Invoke(
                session,
                instance,
                MethodName,
                parameters);

            uint[] fanTable = LenovoWmiScope.ToUInt32Array(
                LenovoCimScope.GetParameter(output, "FanTable"));
            uint[] sensorTable = LenovoWmiScope.ToUInt32Array(
                LenovoCimScope.GetParameter(output, "SensorTable"));
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
        catch (CimException exception)
        {
            throw LenovoCimScope.Map(
                exception,
                "wmi_getter_not_available",
                "wmi_getter_timed_out",
                "wmi_getter_failed");
        }
    }

    private static bool TryReadUInt32(
        CimMethodResult output,
        string name,
        out uint value)
    {
        try
        {
            if (LenovoCimScope.GetParameter(output, name) is uint number)
            {
                value = number;
                return true;
            }
        }
        catch (CimException)
        {
        }

        value = 0;
        return false;
    }
}
