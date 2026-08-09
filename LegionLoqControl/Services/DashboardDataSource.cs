using LegionLoqControl.Application.Diagnostics;
using LegionLoqControl.Contracts.Broker;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Infrastructure.Windows.Broker;
using LegionLoqControl.Infrastructure.Windows.Diagnostics;

namespace LegionLoqControl.Services;

public interface IDashboardDataSource
{
    Task<MachineSnapshot> CaptureMachineAsync(CancellationToken cancellationToken);

    ValueTask<HardwareStateSnapshot> ReadHardwareStateAsync(
        CancellationToken cancellationToken);
}

public sealed class DashboardDataSourceException : Exception
{
    public DashboardDataSourceException(
        string errorCode,
        Exception? innerException = null)
        : base(ValidateErrorCode(errorCode), innerException)
    {
        ErrorCode = errorCode.Trim();
    }

    public string ErrorCode { get; }

    private static string ValidateErrorCode(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        return errorCode.Trim();
    }
}

public sealed class DashboardDataSource : IDashboardDataSource
{
    private readonly MachineDiagnosticsService _diagnostics;
    private readonly Func<CancellationToken, ValueTask<HardwareStateReadResponse>>
        _readHardwareStateAsync;

    public DashboardDataSource()
        : this(
            new MachineDiagnosticsService(
                new WindowsMachineIdentitySource(),
                [new WindowsCapabilityProbe()]),
            static cancellationToken =>
                new ElevatedHardwareStateBrokerClient().ReadAsync(cancellationToken))
    {
    }

    internal DashboardDataSource(
        MachineDiagnosticsService diagnostics,
        Func<CancellationToken, ValueTask<HardwareStateReadResponse>> readHardwareStateAsync)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _readHardwareStateAsync = readHardwareStateAsync ??
            throw new ArgumentNullException(nameof(readHardwareStateAsync));
    }

    public Task<MachineSnapshot> CaptureMachineAsync(CancellationToken cancellationToken) =>
        _diagnostics.CaptureAsync(cancellationToken);

    public async ValueTask<HardwareStateSnapshot> ReadHardwareStateAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            HardwareStateReadResponse response = await _readHardwareStateAsync(cancellationToken)
                .ConfigureAwait(false);
            if (response.Status != BrokerReadStatus.Succeeded || response.Snapshot is null)
            {
                throw new DashboardDataSourceException(
                    response.ErrorCode ?? "broker_read_failed");
            }

            return response.Snapshot.ToSnapshot();
        }
        catch (BrokerTransportException exception)
        {
            throw new DashboardDataSourceException(exception.ErrorCode, exception);
        }
    }
}

