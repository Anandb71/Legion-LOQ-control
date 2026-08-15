namespace LegionLoqControl.Application.Hardware;

public sealed class HardwareWriteGate
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await _lock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new HardwareWriteException(
                "write_in_progress",
                HardwareWriteStatus.Busy);
        }

        return new Releaser(_lock);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                gate.Release();
        }
    }
}
