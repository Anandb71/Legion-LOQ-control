using System.Buffers.Binary;
using System.Text;
using LegionLoqControl.Contracts.Broker;
using Xunit;

namespace LegionLoqControl.Platform.Tests;

public sealed class BrokerWireProtocolTests
{
    [Fact]
    public void Transport_identifiers_are_random_and_strictly_validated()
    {
        string firstNonce = BrokerProtocol.CreateNonce();
        string secondNonce = BrokerProtocol.CreateNonce();
        string pipeName = BrokerProtocol.CreatePipeName();

        Assert.True(BrokerProtocol.IsValidNonce(firstNonce));
        Assert.True(BrokerProtocol.IsValidNonce(secondNonce));
        Assert.NotEqual(firstNonce, secondNonce);
        Assert.True(BrokerProtocol.NoncesEqual(firstNonce, firstNonce));
        Assert.False(BrokerProtocol.NoncesEqual(firstNonce, secondNonce));
        Assert.False(BrokerProtocol.IsValidNonce(firstNonce.ToLowerInvariant()));
        Assert.True(BrokerProtocol.IsValidPipeName(pipeName));
        Assert.False(BrokerProtocol.IsValidPipeName("llc-known-name"));
    }

    [Fact]
    public async Task Request_round_trips_through_a_length_bounded_frame()
    {
        var expected = new HardwareStateReadRequest(
            BrokerProtocol.MajorVersion,
            Guid.NewGuid(),
            new string('a', 64),
            1234);
        using var stream = new MemoryStream();

        await BrokerWireProtocol.WriteAsync(
            stream,
            expected,
            TestContext.Current.CancellationToken);
        stream.Position = 0;
        HardwareStateReadRequest actual = await BrokerWireProtocol.ReadAsync<HardwareStateReadRequest>(
            stream,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Oversized_declared_payload_is_rejected_before_allocation()
    {
        using var stream = CreateFrame(
            BrokerProtocol.MaximumMessageBytes + 1,
            []);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => BrokerWireProtocol.ReadAsync<HardwareStateReadRequest>(
                stream,
                TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task Unknown_json_members_are_rejected()
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            """
            {
              "protocolMajorVersion": 1,
              "requestId": "6bd9f2cf-643d-4f1a-a86b-da25f9a5070b",
              "nonce": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "clientProcessId": 1234,
              "unexpected": true
            }
            """);
        using var stream = CreateFrame(payload.Length, payload);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => BrokerWireProtocol.ReadAsync<HardwareStateReadRequest>(
                stream,
                TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task Integer_enum_values_are_rejected()
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            """
            {
              "protocolMajorVersion": 1,
              "requestId": "6bd9f2cf-643d-4f1a-a86b-da25f9a5070b",
              "status": 0,
              "snapshot": null,
              "errorCode": "invalid"
            }
            """);
        using var stream = CreateFrame(payload.Length, payload);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => BrokerWireProtocol.ReadAsync<HardwareStateReadResponse>(
                stream,
                TestContext.Current.CancellationToken).AsTask());
    }

    private static MemoryStream CreateFrame(int declaredLength, ReadOnlySpan<byte> payload)
    {
        byte[] frame = new byte[sizeof(int) + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, declaredLength);
        payload.CopyTo(frame.AsSpan(sizeof(int)));
        return new MemoryStream(frame, writable: false);
    }
}
