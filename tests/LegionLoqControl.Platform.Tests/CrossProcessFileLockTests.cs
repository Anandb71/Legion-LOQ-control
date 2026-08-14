using LegionLoqControl.Infrastructure.Windows.Storage;
using Xunit;

namespace LegionLoqControl.Platform.Tests;

public sealed class CrossProcessFileLockTests
{
    [Fact]
    public async Task Competing_owner_waits_and_observes_cancellation()
    {
        using var temporary = new TemporaryDirectory();
        var firstLock = new CrossProcessFileLock(temporary.ProtectedFilePath);
        var secondLock = new CrossProcessFileLock(temporary.ProtectedFilePath);
        await using FileStream firstOwner = await firstLock.AcquireAsync(
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => secondLock.AcquireAsync(cancellation.Token).AsTask());

        await firstOwner.DisposeAsync();
        await using FileStream secondOwner = await secondLock.AcquireAsync(
            TestContext.Current.CancellationToken);
        Assert.True(secondOwner.CanRead);
        Assert.True(secondOwner.CanWrite);
    }

    [Fact]
    public async Task Bounded_wait_reports_a_busy_lock()
    {
        using var temporary = new TemporaryDirectory();
        var firstLock = new CrossProcessFileLock(temporary.ProtectedFilePath);
        var secondLock = new CrossProcessFileLock(
            temporary.ProtectedFilePath,
            timeout: TimeSpan.FromMilliseconds(150));
        await using FileStream firstOwner = await firstLock.AcquireAsync(
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<CrossProcessFileLockUnavailableException>(
            () => secondLock
                .AcquireAsync(TestContext.Current.CancellationToken)
                .AsTask());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                "LegionLoqControl.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            ProtectedFilePath = Path.Combine(DirectoryPath, "store.json");
        }

        public string DirectoryPath { get; }

        public string ProtectedFilePath { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
    }
}
