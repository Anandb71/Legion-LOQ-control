using Microsoft.Management.Infrastructure;
using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Domain.Controls;
using LegionLoqControl.Domain.Results;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Hardware;

internal sealed class SystemLenovoFanTableWriteInvoker
{
    internal const string MethodName = "Fan_Set_Table";

    public async ValueTask WriteAsync(
        FanTableSnapshot desired,
        CancellationToken cancellationToken)
    {
        WindowsPlatform.EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();
        if (desired.PointCount is < 1 or > FanTableSnapshot.MaximumPoints ||
            desired.Points is null)
        {
            throw new HardwareWriteException(
                "fan_table_value_invalid",
                HardwareWriteStatus.Failed);
        }

        try
        {
            await Task
                .Run(() => WriteCore(desired), CancellationToken.None)
                .WaitAsync(LenovoCimScope.Timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            throw new HardwareWriteException(
                "fan_table_write_timed_out",
                HardwareWriteStatus.Failed);
        }
        catch (HardwareWriteException)
        {
            throw;
        }
        catch (LenovoWmiReadFailureException exception)
        {
            throw new HardwareWriteException(
                exception.ErrorCode,
                exception.Status == HardwareReadStatus.Unsupported
                    ? HardwareWriteStatus.Unsupported
                    : HardwareWriteStatus.Failed);
        }
        catch (Exception)
        {
            throw new HardwareWriteException(
                "fan_table_write_failed",
                HardwareWriteStatus.Failed);
        }
    }

    private static void WriteCore(FanTableSnapshot desired)
    {
        var table = new byte[desired.PointCount];
        for (int index = 0; index < desired.PointCount; index++)
            table[index] = desired.Points[index].Speed;

        try
        {
            using CimSession session = LenovoCimScope.CreateSession();
            using CimInstance instance = LenovoCimScope.GetInstance(
                session,
                LenovoCimScope.FanMethodClass);
            using var parameters = new CimMethodParametersCollection
            {
                CimMethodParameter.Create("FanTable", table, CimFlags.In),
            };
            using CimMethodResult output = LenovoCimScope.Invoke(
                session,
                instance,
                MethodName,
                parameters);
            object? status = LenovoCimScope.GetParameter(output, "ReturnValue");
            if (status is not null)
                SystemLenovoWmiReadInvoker.ValidateReturnValue(status);
        }
        catch (CimException exception)
        {
            throw LenovoCimScope.Map(
                exception,
                "fan_table_setter_not_available",
                "fan_table_write_timed_out",
                "fan_table_write_failed");
        }
    }
}
