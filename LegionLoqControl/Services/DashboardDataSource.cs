using System.IO;
using LegionLoqControl.Application.Broker;
using LegionLoqControl.Application.Diagnostics;
using LegionLoqControl.Contracts.Broker;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Infrastructure.Windows.Broker;
using LegionLoqControl.Infrastructure.Windows.Diagnostics;

namespace LegionLoqControl.Services;

public interface IDashboardDataSource : IDisposable
{
    Task<MachineSnapshot> CaptureMachineAsync(CancellationToken cancellationToken);

    ValueTask<HardwareStateSnapshot> ReadHardwareStateAsync(
        CancellationToken cancellationToken);

    BrokerInstallAssessment AssessBrokerInstall();

    ValueTask<HardwareStateSnapshot> ApplyHardwareWriteAsync(
        HardwareWriteTarget target,
        string expected,
        string desired,
        CancellationToken cancellationToken);

    ValueTask<HardwareStateSnapshot> ApplyHardwareWriteBatchAsync(
        IReadOnlyList<HardwareWriteOperation> operations,
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
    private readonly Func<BrokerInstallAssessment> _assessBrokerInstall;
    private readonly Func<IReadOnlyList<HardwareWriteOperation>, CancellationToken, ValueTask<HardwareStateWriteResponse>>
        _writeHardwareStateAsync;
    private readonly ElevatedHardwareStateBrokerClient? _broker;

    public DashboardDataSource()
    {
        _broker = new ElevatedHardwareStateBrokerClient();
        _diagnostics = new MachineDiagnosticsService(
            new WindowsMachineIdentitySource(),
            [new WindowsCapabilityProbe()]);
        _readHardwareStateAsync = _broker.ReadAsync;
        _assessBrokerInstall = AssessLocalBrokerInstall;
        _writeHardwareStateAsync = _broker.WriteBatchAsync;
    }

    internal DashboardDataSource(
        MachineDiagnosticsService diagnostics,
        Func<CancellationToken, ValueTask<HardwareStateReadResponse>> readHardwareStateAsync,
        Func<BrokerInstallAssessment>? assessBrokerInstall = null,
        Func<IReadOnlyList<HardwareWriteOperation>, CancellationToken, ValueTask<HardwareStateWriteResponse>>?
            writeHardwareStateAsync = null)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _readHardwareStateAsync = readHardwareStateAsync ??
            throw new ArgumentNullException(nameof(readHardwareStateAsync));
        _assessBrokerInstall = assessBrokerInstall ?? AssessLocalBrokerInstall;
        _writeHardwareStateAsync = writeHardwareStateAsync ??
            ((_, _) => throw new BrokerTransportException("broker_not_found"));
        _broker = null;
    }

    public void Dispose() => _broker?.Dispose();

    private static BrokerInstallAssessment AssessLocalBrokerInstall() =>
        WindowsBrokerInstallInspector.Assess(
            Path.Combine(
                AppContext.BaseDirectory,
                ElevatedHardwareStateBrokerClient.BrokerExecutableName),
            AppContext.BaseDirectory);

    public Task<MachineSnapshot> CaptureMachineAsync(CancellationToken cancellationToken) =>
        _diagnostics.CaptureAsync(cancellationToken);

    public BrokerInstallAssessment AssessBrokerInstall() => _assessBrokerInstall();

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

    public ValueTask<HardwareStateSnapshot> ApplyHardwareWriteAsync(
        HardwareWriteTarget target,
        string expected,
        string desired,
        CancellationToken cancellationToken) =>
        ApplyHardwareWriteBatchAsync(
            [new HardwareWriteOperation(target, expected, desired)],
            cancellationToken);

    public async ValueTask<HardwareStateSnapshot> ApplyHardwareWriteBatchAsync(
        IReadOnlyList<HardwareWriteOperation> operations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        try
        {
            HardwareStateWriteResponse response = await _writeHardwareStateAsync(
                    operations,
                    cancellationToken)
                .ConfigureAwait(false);
            if (response.Status != BrokerCommandStatus.Succeeded || response.Snapshot is null)
            {
                throw new DashboardDataSourceException(
                    response.ErrorCode ?? "broker_write_failed");
            }

            return response.Snapshot.ToSnapshot();
        }
        catch (BrokerTransportException exception)
        {
            throw new DashboardDataSourceException(exception.ErrorCode, exception);
        }
    }
}

