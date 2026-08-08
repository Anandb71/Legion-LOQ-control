using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LegionLoqControl.Contracts.Broker;

public static class BrokerWireProtocol
{
    private const int LengthPrefixBytes = sizeof(int);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
        },
    };

    public static async ValueTask WriteAsync<T>(
        Stream stream,
        T message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);
        if (!stream.CanWrite)
            throw new ArgumentException("The broker stream must be writable.", nameof(stream));

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        if (payload.Length is <= 0 or > BrokerProtocol.MaximumMessageBytes)
            throw new InvalidDataException("The broker message exceeds the protocol limit.");

        byte[] prefix = new byte[LengthPrefixBytes];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<T> ReadAsync<T>(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("The broker stream must be readable.", nameof(stream));

        byte[] prefix = new byte[LengthPrefixBytes];
        await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (payloadLength is <= 0 or > BrokerProtocol.MaximumMessageBytes)
            throw new InvalidDataException("The broker message length is invalid.");

        byte[] payload = GC.AllocateUninitializedArray<byte>(payloadLength);
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);

        try
        {
            return JsonSerializer.Deserialize<T>(payload, SerializerOptions)
                ?? throw new InvalidDataException("The broker message was null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The broker message was not valid protocol JSON.", exception);
        }
    }
}
