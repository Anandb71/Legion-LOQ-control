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
    private static readonly TimeSpan ReadLifetimeTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WriteLifetimeTimeout = TimeSpan.FromSeconds(90);

    public static async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        WindowsPlatform.EnsureSupported();
        if (!BrokerArguments.TryParse(args, out BrokerArguments? options) || options is null)
            return 64;

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(options.Write ? WriteLifetimeTimeout : ReadLifetimeTimeout);

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

            if (options.Write)
            {
                HardwareStateWriteRequest writeRequest = await BrokerWireProtocol
                    .ReadAsync<HardwareStateWriteRequest>(pipe, lifetime.Token)
                    .ConfigureAwait(false);
                BrokerValidationResult writeValidation = BrokerMessageValidator.ValidateWriteRequest(
                    writeRequest,
                    options.Nonce,
                    serverProcessId);
                if (!writeValidation.IsValid)
                {
                    await WriteCommandFailureAsync(
                        pipe,
                        writeRequest.RequestId,
                        BrokerCommandStatus.InvalidRequest,
                        writeValidation.ErrorCode!,
                        lifetime.Token).ConfigureAwait(false);
                    return 0;
                }

                await ExecuteWriteAsync(pipe, writeRequest, lifetime.Token).ConfigureAwait(false);
                return 0;
            }

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

    private static async ValueTask ExecuteWriteAsync(
        Stream stream,
        HardwareStateWriteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!IsElevated())
            {
                await WriteCommandFailureAsync(
                    stream,
                    request.RequestId,
                    BrokerCommandStatus.Failed,
                    "broker_not_elevated",
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            var service = new HardwareStateWriteService(
                WindowsHardwareStateReader.CreatePrivilegedReadOnly,
                new WindowsHardwareStateWriter());
            (HardwareWriteKind Kind, string Expected, string Desired)[] operations =
                request.Operations
                    .Select(static operation => (
                        MapWriteKind(operation.Target),
                        operation.Expected,
                        operation.Desired))
                    .ToArray();
            HardwareStateSnapshot snapshot = operations.Length == 1
                ? await service
                    .ApplyAsync(
                        operations[0].Kind,
                        operations[0].Expected,
                        operations[0].Desired,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await service
                    .ApplyManyAsync(operations, cancellationToken)
                    .ConfigureAwait(false);
            var response = new HardwareStateWriteResponse(
                BrokerProtocol.MajorVersion,
                request.RequestId,
                BrokerCommandStatus.Succeeded,
                HardwareStateReadPayload.FromSnapshot(snapshot),
                null);
            await BrokerWireProtocol
                .WriteAsync(stream, response, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HardwareWriteException exception)
        {
            await WriteCommandFailureAsync(
                stream,
                request.RequestId,
                MapWriteStatus(exception.Status),
                exception.ErrorCode,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await WriteCommandFailureAsync(
                stream,
                request.RequestId,
                BrokerCommandStatus.Failed,
                ClassifyFailure(exception),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static HardwareWriteKind MapWriteKind(HardwareWriteTarget target) =>
        target switch
        {
            HardwareWriteTarget.ThermalMode => HardwareWriteKind.ThermalMode,
            HardwareWriteTarget.DisplayOverdrive => HardwareWriteKind.DisplayOverdrive,
            HardwareWriteTarget.IntegratedGpuMode => HardwareWriteKind.IntegratedGpuMode,
            HardwareWriteTarget.BatteryChargeMode => HardwareWriteKind.BatteryChargeMode,
            HardwareWriteTarget.FourZoneKeyboard => HardwareWriteKind.FourZoneKeyboard,
            _ => throw new HardwareWriteException(
                "write_target_invalid",
                HardwareWriteStatus.Failed),
        };

    private static BrokerCommandStatus MapWriteStatus(HardwareWriteStatus status) =>
        status switch
        {
            HardwareWriteStatus.Unsupported => BrokerCommandStatus.Unsupported,
            HardwareWriteStatus.Conflict => BrokerCommandStatus.Conflict,
            HardwareWriteStatus.Unverified => BrokerCommandStatus.Unverified,
            HardwareWriteStatus.Busy => BrokerCommandStatus.Busy,
            _ => BrokerCommandStatus.Failed,
        };

    private static async ValueTask WriteCommandFailureAsync(
        Stream stream,
        Guid requestId,
        BrokerCommandStatus status,
        string errorCode,
        CancellationToken cancellationToken)
    {
        var response = new HardwareStateWriteResponse(
            BrokerProtocol.MajorVersion,
            requestId,
            status,
            null,
            errorCode);
        await BrokerWireProtocol
            .WriteAsync(stream, response, cancellationToken)
            .ConfigureAwait(false);
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
