using System.IO.Pipes;
using System.Security.Principal;
using LegionLoqControl.Application.Hardware;
using LegionLoqControl.Contracts.Broker;
using LegionLoqControl.Domain.Diagnostics;
using LegionLoqControl.Infrastructure.Windows.Broker;
using LegionLoqControl.Infrastructure.Windows.Hardware;
using LegionLoqControl.Infrastructure.Windows.Platform;

namespace LegionLoqControl.Broker;

internal static class BrokerHost
{
    private static readonly TimeSpan LifetimeTimeout = TimeSpan.FromSeconds(30);

    public static async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        WindowsPlatform.EnsureSupported();
        if (!BrokerArguments.TryParse(args, out BrokerArguments? options) || options is null)
            return 64;

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(LifetimeTimeout);

        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                options.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous,
                TokenImpersonationLevel.Anonymous);
            await pipe.ConnectAsync(lifetime.Token).ConfigureAwait(false);

            int serverProcessId = NamedPipePeerProcess.GetServerProcessId(pipe);
            if (serverProcessId != options.ParentProcessId)
                return 77;

            HardwareStateReadRequest request = await BrokerWireProtocol
                .ReadAsync<HardwareStateReadRequest>(pipe, lifetime.Token)
                .ConfigureAwait(false);
            BrokerValidationResult validation = BrokerMessageValidator.ValidateRequest(
                request,
                options.Nonce,
                serverProcessId);
            if (!validation.IsValid)
            {
                await WriteFailureAsync(
                    pipe,
                    request.RequestId,
                    validation.Status,
                    validation.ErrorCode!,
                    lifetime.Token).ConfigureAwait(false);
                return 0;
            }

            await ExecuteReadAsync(pipe, request, lifetime.Token).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            return 2;
        }
        catch (InvalidDataException)
        {
            return 65;
        }
        catch (EndOfStreamException)
        {
            return 66;
        }
        catch (NotSupportedException)
        {
            return 69;
        }
        catch (InvalidOperationException)
        {
            return 70;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return 71;
        }
        catch (IOException)
        {
            return 74;
        }
        catch (UnauthorizedAccessException)
        {
            return 77;
        }
        catch (System.Security.SecurityException)
        {
            return 77;
        }
        catch (Exception)
        {
            return 1;
        }
    }

    private static async ValueTask ExecuteReadAsync(
        Stream stream,
        HardwareStateReadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!IsElevated())
            {
                await WriteFailureAsync(
                    stream,
                    request.RequestId,
                    BrokerReadStatus.Unauthorized,
                    "broker_not_elevated",
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            var service = new HardwareStateService(
                WindowsHardwareStateReader.CreatePrivilegedReadOnly());
            HardwareStateSnapshot snapshot = await service
                .CaptureAsync(cancellationToken)
                .ConfigureAwait(false);
            var response = new HardwareStateReadResponse(
                BrokerProtocol.MajorVersion,
                request.RequestId,
                BrokerReadStatus.Succeeded,
                HardwareStateReadPayload.FromSnapshot(snapshot),
                null);
            await BrokerWireProtocol
                .WriteAsync(stream, response, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await WriteFailureAsync(
                stream,
                request.RequestId,
                BrokerReadStatus.Failed,
                ClassifyFailure(exception),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static string ClassifyFailure(Exception exception) =>
        exception switch
        {
            System.ComponentModel.Win32Exception => "broker_win32_failed",
            System.Security.SecurityException => "broker_security_failed",
            InvalidDataException => "broker_data_failed",
            InvalidOperationException => "broker_operation_failed",
            IOException => "broker_io_failed",
            _ => "broker_internal_failed",
        };

    private static async ValueTask WriteFailureAsync(
        Stream stream,
        Guid requestId,
        BrokerReadStatus status,
        string errorCode,
        CancellationToken cancellationToken)
    {
        var response = new HardwareStateReadResponse(
            BrokerProtocol.MajorVersion,
            requestId,
            status,
            null,
            errorCode);
        await BrokerWireProtocol
            .WriteAsync(stream, response, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
