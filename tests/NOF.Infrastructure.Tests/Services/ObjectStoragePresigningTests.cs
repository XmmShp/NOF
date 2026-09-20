using Microsoft.Extensions.Options;
using NOF.Application;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public sealed class ObjectStoragePresigningTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalProviders_ShouldExplicitlyRejectPresigning(bool useFileSystem)
    {
        using var memory = new MemoryObjectStorageRider();
        IObjectStorageRider rider = useFileSystem
            ? new FileSystemObjectStorageRider(Options.Create(new FileSystemObjectStorageOptions()))
            : memory;
        IObjectStorage storage = new ObjectStorageService(rider, Options.Create(new ObjectStorageOptions()));
        Assert.False(rider.SupportsPresignedRequests);
        Assert.False(storage.SupportsPresignedRequests);
        Assert.False(storage.IgnoreKeyPrefix().SupportsPresignedRequests);
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await storage.CreatePresignedUploadAsync("bucket", "key", TimeSpan.FromMinutes(5)));
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await storage.CreatePresignedDownloadAsync("bucket", "key", TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task Service_ShouldValidateArgumentsAndCancellationBeforeCallingProvider()
    {
        using var rider = new MemoryObjectStorageRider();
        var storage = new ObjectStorageService(rider, Options.Create(new ObjectStorageOptions()));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await storage.CreatePresignedUploadAsync("", "key", TimeSpan.FromMinutes(5)));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await storage.CreatePresignedDownloadAsync("bucket", " ", TimeSpan.FromMinutes(5)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await storage.CreatePresignedUploadAsync("bucket", "key", TimeSpan.Zero));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await storage.CreatePresignedDownloadAsync("bucket", "key", TimeSpan.Zero));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await storage.CreatePresignedDownloadAsync("bucket", "key", TimeSpan.FromMinutes(5), cancellation.Token));
    }
}
