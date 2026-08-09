using LegionLoqControl.Domain.Profiles;

namespace LegionLoqControl.Application.Profiles;

public interface IProfileStore
{
    ValueTask<IReadOnlyList<HardwareProfile>> LoadAsync(
        CancellationToken cancellationToken = default);

    ValueTask SaveAsync(
        HardwareProfile profile,
        CancellationToken cancellationToken = default);

    ValueTask<bool> DeleteAsync(
        ProfileId id,
        CancellationToken cancellationToken = default);
}

public sealed class ProfileStoreException : Exception
{
    public ProfileStoreException(string errorCode, Exception? innerException = null)
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

