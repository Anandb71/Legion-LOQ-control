using System.IO;
using LegionLoqControl.Application.Broker;
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

    BrokerInstallAssessment AssessBrokerInstall();

    ValueTask<HardwareStateSnapshot> ApplyHardwareWriteAsync(
        HardwareWriteTarget target,
        string expected,
        string desired,
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
    private readonly Func<HardwareWriteTarget, string, string, CancellationToken, ValueTask<HardwareStateWriteResponse>>
        _writeHardwareStateAsync;

    public DashboardDataSource()
        : this(
            new MachineDiagnosticsService(
                new WindowsMachineIdentitySource(),
                [new WindowsCapabilityProbe()]),
            static cancellationToken =>
                new ElevatedHardwareStateBrokerClient().ReadAsync(cancellationToken),
            AssessLocalBrokerInstall,
            WriteLocalBroker)
    {
    }

    internal DashboardDataSource(
        MachineDiagnosticsService diagnostics,
        Func<CancellationToken, ValueTask<HardwareStateReadResponse>> readHardwareStateAsync,
        Func<BrokerInstallAssessment>? assessBrokerInstall = null,
        Func<HardwareWriteTarget, string, string, CancellationToken, ValueTask<HardwareStateWriteResponse>>?
            writeHardwareStateAsync = null)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _readHardwareStateAsync = readHardwareStateAsync ??
            throw new ArgumentNullException(nameof(readHardwareStateAsync));
        _assessBrokerInstall = assessBrokerInstall ?? AssessLocalBrokerInstall;
        _writeHardwareStateAsync = writeHardwareStateAsync ?? WriteLocalBroker;
    }

    private static ValueTask<HardwareStateWriteResponse> WriteLocalBroker(
        HardwareWriteTarget target,
        string expected,
        string desired,
        CancellationToken cancellationToken) =>
        new ElevatedHardwareStateBrokerClient().WriteAsync(
            target,
            expected,
            desired,
            cancellationToken);

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

    public async ValueTask<HardwareStateSnapshot> ApplyHardwareWriteAsync(
        HardwareWriteTarget target,
        string expected,
        string desired,
        CancellationToken cancellationToken)
    {
        try
        {
            HardwareStateWriteResponse response = await _writeHardwareStateAsync(
                    target,
                    expected,
                    desired,
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

