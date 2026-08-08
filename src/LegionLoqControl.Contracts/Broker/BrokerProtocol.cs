using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace LegionLoqControl.Contracts.Broker;

public static class BrokerProtocol
{
    public const ushort MajorVersion = 1;
    public const int MaximumMessageBytes = 64 * 1024;
    public const int NonceBytes = 32;
    public const int NonceCharacters = NonceBytes * 2;
    public const string PipeNamePrefix = "llc-";

    public static string CreateNonce() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(NonceBytes));

    public static bool IsValidNonce(string? value) =>
        value is { Length: NonceCharacters } &&
        value.All(static character =>
            character is >= '0' and <= '9' or >= 'A' and <= 'F');

    public static bool NoncesEqual(string? left, string? right) =>
        IsValidNonce(left) &&
        IsValidNonce(right) &&
        CryptographicOperations.FixedTimeEquals(
            MemoryMarshal.AsBytes(left.AsSpan()),
            MemoryMarshal.AsBytes(right.AsSpan()));

    public static bool IsValidPipeName(string? value) =>
        value is not null &&
        value.Length == PipeNamePrefix.Length + 32 &&
        value.StartsWith(PipeNamePrefix, StringComparison.Ordinal) &&
        IsLowercaseHex(value.AsSpan(PipeNamePrefix.Length));

    public static string CreatePipeName() =>
        $"{PipeNamePrefix}{Guid.NewGuid():N}";

    private static bool IsLowercaseHex(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (character is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
                return false;
        }

        return true;
    }
}

public sealed record CommandId
{
    public CommandId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Command IDs cannot be empty.", nameof(value));

        Value = value;
    }

    public Guid Value { get; }

    public static CommandId New() => new(Guid.NewGuid());
}
