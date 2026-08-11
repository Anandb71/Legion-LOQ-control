namespace LegionLoqControl.Application.Diagnostics;

public enum DiagnosticsExportWriteMode
{
    CreateNew = 0,
    ReplaceExisting = 1,
}

public interface IDiagnosticsExportWriter
{
    ValueTask WriteAsync(
        DiagnosticsExportDocument document,
        string destinationPath,
        DiagnosticsExportWriteMode mode,
        CancellationToken cancellationToken = default);
}

public sealed class DiagnosticsExportException : Exception
{
    public DiagnosticsExportException(string errorCode, Exception? innerException = null)
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
