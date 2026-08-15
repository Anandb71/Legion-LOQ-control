using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using LegionLoqControl.Application.Broker;
using LegionLoqControl.Contracts.Broker;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Infrastructure.Windows.Broker;

public sealed class ElevatedHardwareStateBrokerClient : IDisposable
{
    public const string BrokerExecutableName = "LegionLoqControl.Broker.exe";

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan WriteRequestTimeout = TimeSpan.FromSeconds(90);
    private readonly string _brokerExecutablePath;
    private readonly BrokerInstallMode _installMode;
    private readonly Func<string, string, BrokerInstallAssessment> _assessInstall;
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private NamedPipeServerStream? _pipe;
    private Process? _process;
    private string? _nonce;
    private bool _disposed;

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

    public bool IsSessionConnected =>
        _pipe is { IsConnected: true } &&
        _process is { HasExited: false };

    public async ValueTask<HardwareStateReadResponse> ReadAsync(
        CancellationToken cancellationToken)
    {
        HardwareStateReadRequest request = CreateIdentity();
        BrokerSessionResponse response = await SendAsync(
            new BrokerSessionRequest(BrokerSessionKind.Read, request, null),
            RequestTimeout,
            cancellationToken).ConfigureAwait(false);
        return response.Read
            ?? throw new BrokerTransportException("broker_response_invalid");
    }

    public ValueTask<HardwareStateWriteResponse> WriteAsync(
        HardwareWriteTarget target,
        string expected,
        string desired,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expected);
        ArgumentException.ThrowIfNullOrWhiteSpace(desired);
        if (!Enum.IsDefined(target))
            throw new ArgumentOutOfRangeException(nameof(target));

        return WriteBatchAsync(
            [new HardwareWriteOperation(target, expected.Trim(), desired.Trim())],
            cancellationToken);
    }

    public async ValueTask<HardwareStateWriteResponse> WriteBatchAsync(
        IReadOnlyList<HardwareWriteOperation> operations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Count is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(operations));
        foreach (HardwareWriteOperation operation in operations)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operation.Expected);
            ArgumentException.ThrowIfNullOrWhiteSpace(operation.Desired);
            if (!Enum.IsDefined(operation.Target))
                throw new ArgumentOutOfRangeException(nameof(operations));
        }

        HardwareStateReadRequest identity = CreateIdentity();
        var request = new HardwareStateWriteRequest(
            identity.ProtocolMajorVersion,
            identity.RequestId,
            identity.Nonce,
            identity.ClientProcessId,
            operations);
        BrokerSessionResponse response = await SendAsync(
            new BrokerSessionRequest(BrokerSessionKind.Write, null, request),
            WriteRequestTimeout,
            cancellationToken).ConfigureAwait(false);
        return response.Write
            ?? throw new BrokerTransportException("broker_response_invalid");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        TryTerminate(_process);
        _process?.Dispose();
        _pipe?.Dispose();
        _sessionGate.Dispose();
    }

    private HardwareStateReadRequest CreateIdentity() =>
        new(
            BrokerProtocol.MajorVersion,
            Guid.NewGuid(),
            _nonce ?? BrokerProtocol.CreateNonce(),
            Environment.ProcessId);

    private async ValueTask<BrokerSessionResponse> SendAsync(
        BrokerSessionRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        WindowsPlatform.EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureConnectedUnlockedAsync(cancellationToken).ConfigureAwait(false);
            if (_nonce is not null)
            {
                request = request.Kind switch
                {
                    BrokerSessionKind.Read when request.Read is { } read =>
                        request with
                        {
                            Read = read with { Nonce = _nonce },
                        },
                    BrokerSessionKind.Write when request.Write is { } write =>
                        request with
                        {
                            Write = write with { Nonce = _nonce },
                        },
                    BrokerSessionKind.Shutdown when request.Read is { } shutdown =>
                        request with
                        {
                            Read = shutdown with { Nonce = _nonce },
                        },
                    _ => request,
                };
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(timeout);
            return await BrokerPipeExchange
                .ExchangeSessionAsync(_pipe!, request, linked.Token)
                .ConfigureAwait(false);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            DisconnectUnlocked();
            throw new BrokerTransportException("broker_elevation_cancelled", exception);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            DisconnectUnlocked();
            throw new BrokerTransportException("broker_timeout");
        }
        catch (UnauthorizedAccessException exception)
        {
            DisconnectUnlocked();
            throw new BrokerTransportException("broker_peer_mismatch", exception);
        }
        catch (InvalidDataException exception)
        {
            DisconnectUnlocked();
            throw new BrokerTransportException("broker_response_invalid", exception);
        }
        catch (IOException exception)
        {
            string errorCode = ResolvePipeFailureCode(_process);
            DisconnectUnlocked();
            throw new BrokerTransportException(errorCode, exception);
        }
        catch (Win32Exception exception)
        {
            DisconnectUnlocked();
            throw new BrokerTransportException("broker_transport_failed", exception);
        }
    }

    private async ValueTask EnsureConnectedUnlockedAsync(CancellationToken cancellationToken)
    {
        if (IsSessionConnected)
            return;

        DisconnectUnlocked();
        BrokerInstallAssessment install = AssessInstall();
        if (!BrokerInstallPolicy.Allows(install, _installMode))
        {
            throw new BrokerTransportException(
                BrokerInstallPolicy.RefusalCode(install, _installMode));
        }

        string nonce = BrokerProtocol.CreateNonce();
        string pipeName = BrokerProtocol.CreatePipeName();
        NamedPipeServerStream pipe = BrokerPipeFactory.CreateServer(pipeName);
        Process? process = null;
        try
        {
            process = LaunchBroker(pipeName, nonce, Environment.ProcessId);
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            connectTimeout.CancelAfter(ConnectTimeout);
            await BrokerPipeExchange
                .ConnectAsync(pipe, process.Id, connectTimeout.Token)
                .ConfigureAwait(false);
            _pipe = pipe;
            _process = process;
            _nonce = nonce;
        }
        catch
        {
            TryTerminate(process);
            process?.Dispose();
            pipe.Dispose();
            throw;
        }
    }

    private void DisconnectUnlocked()
    {
        TryTerminate(_process);
        _process?.Dispose();
        _pipe?.Dispose();
        _process = null;
        _pipe = null;
        _nonce = null;
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
        startInfo.ArgumentList.Add("--session");

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
            // The elevated process may deny termination; it exits when the parent closes.
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
}
