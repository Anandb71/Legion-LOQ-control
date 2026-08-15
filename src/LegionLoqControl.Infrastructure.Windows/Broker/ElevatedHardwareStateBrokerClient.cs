using System.ComponentModel;
using System.Diagnostics;
using LegionLoqControl.Application.Broker;
using LegionLoqControl.Contracts.Broker;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Broker;

public sealed class ElevatedHardwareStateBrokerClient
{
    public const string BrokerExecutableName = "LegionLoqControl.Broker.exe";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ResponseDrainTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(5);
    private readonly string _brokerExecutablePath;
    private readonly BrokerInstallMode _installMode;
    private readonly Func<string, string, BrokerInstallAssessment> _assessInstall;

    public ElevatedHardwareStateBrokerClient()
        : this(Path.Combine(AppContext.BaseDirectory, BrokerExecutableName))
    {
    }

    internal ElevatedHardwareStateBrokerClient(string brokerExecutablePath)
        : this(
            brokerExecutablePath,
            BrokerInstallPolicy.ResolveMode(
                Environment.GetEnvironmentVariable("LEGIONLOQ_BROKER_INSTALL_MODE")))
    {
    }

    internal ElevatedHardwareStateBrokerClient(
        string brokerExecutablePath,
        BrokerInstallMode installMode,
        Func<string, string, BrokerInstallAssessment>? assessInstall = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerExecutablePath);
        if (!Enum.IsDefined(installMode))
            throw new ArgumentOutOfRangeException(nameof(installMode));

        _brokerExecutablePath = ValidateBrokerPath(brokerExecutablePath);
        _installMode = installMode;
        _assessInstall = assessInstall ?? WindowsBrokerInstallInspector.Assess;
    }

    public BrokerInstallAssessment AssessInstall() =>
        _assessInstall(_brokerExecutablePath, AppContext.BaseDirectory);

    public async ValueTask<HardwareStateReadResponse> ReadAsync(
        CancellationToken cancellationToken)
    {
        WindowsPlatform.EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();

        BrokerInstallAssessment install = AssessInstall();
        if (!BrokerInstallPolicy.Allows(install, _installMode))
        {
            throw new BrokerTransportException(
                BrokerInstallPolicy.RefusalCode(install, _installMode));
        }

        Guid requestId = Guid.NewGuid();
        string nonce = BrokerProtocol.CreateNonce();
        string pipeName = BrokerProtocol.CreatePipeName();
        var request = new HardwareStateReadRequest(
            BrokerProtocol.MajorVersion,
            requestId,
            nonce,
            Environment.ProcessId);

        using var pipe = BrokerPipeFactory.CreateServer(pipeName);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        Process? process = null;
        try
        {
            process = LaunchBroker(pipeName, nonce, request.ClientProcessId);
            Task<HardwareStateReadResponse> exchange = BrokerPipeExchange
                .ExchangeAsync(pipe, process.Id, request, timeout.Token)
                .AsTask();
            Task processExit = process.WaitForExitAsync(CancellationToken.None);
            Task firstCompleted = await Task
                .WhenAny(exchange, processExit)
                .ConfigureAwait(false);
            HardwareStateReadResponse response;
            if (firstCompleted == processExit && !exchange.IsCompleted)
            {
                try
                {
                    response = await exchange
                        .WaitAsync(ResponseDrainTimeout, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    string errorCode = ResolvePipeFailureCode(process);
                    timeout.Cancel();
                    await ObserveFailureAsync(exchange).ConfigureAwait(false);
                    throw new BrokerTransportException(errorCode);
                }
            }
            else
            {
                response = await exchange.ConfigureAwait(false);
            }

            await processExit
                .WaitAsync(ExitTimeout, CancellationToken.None)
                .ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new BrokerTransportException("broker_exit_failed");

            return response;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            throw new BrokerTransportException("broker_elevation_cancelled", exception);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryTerminate(process);
            throw;
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested &&
            timeout.IsCancellationRequested)
        {
            TryTerminate(process);
            throw new BrokerTransportException("broker_timeout");
        }
        catch (UnauthorizedAccessException exception)
        {
            TryTerminate(process);
            throw new BrokerTransportException("broker_peer_mismatch", exception);
        }
        catch (InvalidDataException exception)
        {
            TryTerminate(process);
            throw new BrokerTransportException("broker_response_invalid", exception);
        }
        catch (TimeoutException exception)
        {
            TryTerminate(process);
            throw new BrokerTransportException("broker_exit_timeout", exception);
        }
        catch (IOException exception)
        {
            string errorCode = ResolvePipeFailureCode(process);
            TryTerminate(process);
            throw new BrokerTransportException(errorCode, exception);
        }
        catch (Win32Exception exception)
        {
            TryTerminate(process);
            throw new BrokerTransportException("broker_transport_failed", exception);
        }
        finally
        {
            process?.Dispose();
        }
    }

    private Process LaunchBroker(
        string pipeName,
        string nonce,
        int parentProcessId)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _brokerExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(_brokerExecutablePath)!,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        startInfo.ArgumentList.Add("--pipe");
        startInfo.ArgumentList.Add(pipeName);
        startInfo.ArgumentList.Add("--nonce");
        startInfo.ArgumentList.Add(nonce);
        startInfo.ArgumentList.Add("--parent-pid");
        startInfo.ArgumentList.Add(parentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));

        try
        {
            return Process.Start(startInfo)
                ?? throw new BrokerTransportException("broker_launch_failed");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            throw;
        }
        catch (Win32Exception exception)
        {
            throw new BrokerTransportException("broker_launch_failed", exception);
        }
        catch (Exception exception)
        {
            throw new BrokerTransportException("broker_launch_failed", exception);
        }
    }

    private static string ValidateBrokerPath(string value)
    {
        string fullPath = Path.GetFullPath(value);
        if (!string.Equals(
                Path.GetFileName(fullPath),
                BrokerExecutableName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The broker executable name is invalid.", nameof(value));
        }

        string expectedDirectory = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(AppContext.BaseDirectory));
        string actualDirectory = Path.TrimEndingDirectorySeparator(
            Path.GetDirectoryName(fullPath)
                ?? throw new ArgumentException("The broker path has no directory.", nameof(value)));
        if (!string.Equals(expectedDirectory, actualDirectory, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The broker must be beside the client executable.", nameof(value));
        if (!File.Exists(fullPath))
            throw new BrokerTransportException("broker_not_found");

        return fullPath;
    }

    private static void TryTerminate(Process? process)
    {
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
            // The elevated process may deny termination; it has its own bounded timeout.
        }
    }

    private static string ResolvePipeFailureCode(Process? process)
    {
        if (process is null)
            return "broker_transport_failed";

        try
        {
            _ = process.WaitForExit(milliseconds: 1000);
            if (!process.HasExited)
                return "broker_transport_failed";

            return process.ExitCode switch
            {
                0 => "broker_closed_without_response",
                1 => "broker_internal_failed",
                2 => "broker_lifetime_timeout",
                64 => "broker_arguments_invalid",
                65 => "broker_request_invalid",
                66 => "broker_request_truncated",
                69 => "broker_operation_not_supported",
                70 => "broker_operation_invalid",
                71 => "broker_win32_failed",
                74 => "broker_io_failed",
                77 => "broker_authorization_failed",
                _ => "broker_exit_failed",
            };
        }
        catch (Exception)
        {
            return "broker_transport_failed";
        }
    }

    private static async Task ObserveFailureAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The exchange task is observed here; the early process exit is reported instead.
        }
    }
}
