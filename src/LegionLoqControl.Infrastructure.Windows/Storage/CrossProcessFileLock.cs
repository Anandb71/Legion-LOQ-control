namespace LegionLoqControl.Infrastructure.Windows.Storage;

internal sealed class CrossProcessFileLock
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(25);

    private readonly string _lockPath;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _timeout;

    public CrossProcessFileLock(
        string protectedFilePath,
        TimeProvider? timeProvider = null,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedFilePath);

        string fullPath = Path.GetFullPath(protectedFilePath);
        if (string.IsNullOrWhiteSpace(Path.GetFileName(fullPath)))
        {
            throw new ArgumentException(
                "A protected file path must include a file name.",
                nameof(protectedFilePath));
        }

        _lockPath = fullPath + ".lock";
        _timeProvider = timeProvider ?? TimeProvider.System;
        _timeout = timeout ?? DefaultTimeout;
        if (_timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public async ValueTask<FileStream> AcquireAsync(
        CancellationToken cancellationToken = default)
    {
        long startedAt = _timeProvider.GetTimestamp();
        IOException? lastException = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    _lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.None);
            }
            catch (DirectoryNotFoundException)
            {
                throw;
            }
            catch (IOException exception)
            {
                lastException = exception;
            }

            TimeSpan elapsed = _timeProvider.GetElapsedTime(startedAt);
            TimeSpan remaining = _timeout - elapsed;
            if (remaining <= TimeSpan.Zero)
                throw new CrossProcessFileLockUnavailableException(lastException!);

            TimeSpan delay = remaining < RetryDelay ? remaining : RetryDelay;
            await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }
}

internal sealed class CrossProcessFileLockUnavailableException(Exception innerException)
    : Exception("The protected file is busy.", innerException);
