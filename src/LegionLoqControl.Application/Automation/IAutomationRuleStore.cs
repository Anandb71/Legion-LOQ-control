using LegionLoqControl.Domain.Automation;

namespace LegionLoqControl.Application.Automation;

public interface IAutomationRuleStore
{
    ValueTask<IReadOnlyList<AutomationRule>> LoadAsync(
        CancellationToken cancellationToken = default);

    ValueTask SaveAsync(
        AutomationRule rule,
        CancellationToken cancellationToken = default);

    ValueTask<bool> DeleteAsync(
        AutomationRuleId id,
        CancellationToken cancellationToken = default);
}

public sealed class AutomationRuleStoreException : Exception
{
    public AutomationRuleStoreException(string errorCode, Exception? innerException = null)
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
