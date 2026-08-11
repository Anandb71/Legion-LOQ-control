using System.Text.Json;
using LegionLoqControl.Application.Diagnostics;

namespace LegionLoqControl.Infrastructure.Windows.Diagnostics;

public sealed class JsonDiagnosticsExportWriter : IDiagnosticsExportWriter
{
    internal const long MaximumDocumentBytes = 256 * 1024;

    public async ValueTask WriteAsync(
        DiagnosticsExportDocument document,
        string destinationPath,
        DiagnosticsExportWriteMode mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));

        string fullPath = NormalizeDestination(destinationPath);
        string directory = Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(directory))
        {
            throw new DiagnosticsExportException(
                "diagnostics_export_directory_missing");
        }

        byte[] content;
        try
        {
            content = DiagnosticsJsonSerializer.Serialize(document);
        }
        catch (JsonException exception)
        {
            throw new DiagnosticsExportException(
                "diagnostics_export_serialization_failed",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new DiagnosticsExportException(
                "diagnostics_export_serialization_failed",
                exception);
        }

        if (content.LongLength > MaximumDocumentBytes)
            throw new DiagnosticsExportException("diagnostics_export_too_large");

        if (mode == DiagnosticsExportWriteMode.CreateNew && File.Exists(fullPath))
            throw new DiagnosticsExportException("diagnostics_export_destination_exists");

        cancellationToken.ThrowIfCancellationRequested();

        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(
                temporaryPath,
                fullPath,
                overwrite: mode == DiagnosticsExportWriteMode.ReplaceExisting);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new DiagnosticsExportException(
                "diagnostics_export_access_denied",
                exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new DiagnosticsExportException(
                "diagnostics_export_directory_missing",
                exception);
        }
        catch (IOException exception)
        {
            string errorCode =
                mode == DiagnosticsExportWriteMode.CreateNew && File.Exists(fullPath)
                    ? "diagnostics_export_destination_exists"
                    : "diagnostics_export_write_failed";
            throw new DiagnosticsExportException(errorCode, exception);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private static string NormalizeDestination(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        try
        {
            string normalized = destinationPath.Trim();
            if (!Path.IsPathFullyQualified(normalized))
                throw new DiagnosticsExportException("diagnostics_export_destination_invalid");

            string fullPath = Path.GetFullPath(normalized);
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(Path.GetFileName(fullPath)) ||
                string.IsNullOrWhiteSpace(Path.GetDirectoryName(fullPath)))
            {
                throw new DiagnosticsExportException(
                    "diagnostics_export_destination_invalid");
            }

            return fullPath;
        }
        catch (DiagnosticsExportException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            throw new DiagnosticsExportException(
                "diagnostics_export_destination_invalid",
                exception);
        }
    }

    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
