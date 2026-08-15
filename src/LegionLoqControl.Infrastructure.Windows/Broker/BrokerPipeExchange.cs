using System.IO.Pipes;
using LegionLoqControl.Contracts.Broker;

namespace LegionLoqControl.Infrastructure.Windows.Broker;

internal static class BrokerPipeExchange
{
    public static async ValueTask<HardwareStateReadResponse> ExchangeAsync(
        NamedPipeServerStream server,
        int expectedBrokerProcessId,
        HardwareStateReadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedBrokerProcessId);
        if (server.IsConnected)
            throw new InvalidOperationException("The broker pipe already has a client.");

        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        int connectedProcessId = NamedPipePeerProcess.GetClientProcessId(server);
        if (connectedProcessId != expectedBrokerProcessId)
            throw new UnauthorizedAccessException("An unexpected process connected to the broker pipe.");

        await BrokerWireProtocol
            .WriteAsync(server, request, cancellationToken)
            .ConfigureAwait(false);
        HardwareStateReadResponse response = await BrokerWireProtocol
            .ReadAsync<HardwareStateReadResponse>(server, cancellationToken)
            .ConfigureAwait(false);
        BrokerMessageValidator.ValidateResponse(response, request.RequestId);
        return response;
    }

    public static async ValueTask<HardwareStateWriteResponse> ExchangeWriteAsync(
        NamedPipeServerStream server,
        int expectedBrokerProcessId,
        HardwareStateWriteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedBrokerProcessId);
        if (server.IsConnected)
            throw new InvalidOperationException("The broker pipe already has a client.");

        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        int connectedProcessId = NamedPipePeerProcess.GetClientProcessId(server);
        if (connectedProcessId != expectedBrokerProcessId)
            throw new UnauthorizedAccessException("An unexpected process connected to the broker pipe.");

        await BrokerWireProtocol
            .WriteAsync(server, request, cancellationToken)
            .ConfigureAwait(false);
        HardwareStateWriteResponse response = await BrokerWireProtocol
            .ReadAsync<HardwareStateWriteResponse>(server, cancellationToken)
            .ConfigureAwait(false);
        BrokerMessageValidator.ValidateWriteResponse(response, request.RequestId);
        return response;
    }
}
