using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Hosting;
using NOF.Test;
using System.Text;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public sealed class FileSystemObjectStorageRiderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NOF-object-storage-tests", Guid.NewGuid().ToString("N"));
    private FileSystemObjectStorageRider CreateRider() => new(Options.Create(new FileSystemObjectStorageOptions { RootPath = _root }));

    [Fact]
    public async Task ContentAndMetadata_ShouldPersistAcrossInstances()
    {
        var rider = CreateRider();
        using var input = new MemoryStream("hello"u8.ToArray());
        var stored = await rider.PutAsync("bucket", "nested/file", input, new ObjectStorageWriteOptions
        {
            ContentType = "text/plain", ContentEncoding = "utf-8", CacheControl = "private",
            ContentDisposition = "attachment", Metadata = new Dictionary<string, string> { ["Key"] = "value" }
        });
        Assert.True(input.CanRead);
        var reopened = CreateRider();
        var result = (await reopened.OpenReadAsync("bucket", "nested/file")).Value;
        await using var content = result.Content;
        Assert.Equal(5, content.Length);
        Assert.Equal(0, content.Position);
        Assert.Equal(stored.EntityTag, result.ObjectInfo.EntityTag);
        Assert.Equal(stored.LastModified, result.ObjectInfo.LastModified);
        Assert.Equal("text/plain", result.ObjectInfo.ContentType);
        Assert.Equal("utf-8", result.ObjectInfo.ContentEncoding);
        Assert.Equal("private", result.ObjectInfo.CacheControl);
        Assert.Equal("attachment", result.ObjectInfo.ContentDisposition);
        Assert.Equal("value", result.ObjectInfo.Metadata["KEY"]);
        using var reader = new StreamReader(content, leaveOpen: true);
        Assert.Equal("hello", await reader.ReadToEndAsync());
        content.Seek(0, SeekOrigin.Begin);
        Assert.Equal((int)'h', content.ReadByte());
        Assert.Throws<IOException>(() => content.Seek(-1, SeekOrigin.Begin));
    }

    [Fact]
    public async Task CopyListDeleteAndOverwrite_ShouldKeepOpenReadSnapshot()
    {
        var rider = CreateRider();
        await Put(rider, "source", "reports/b", "old");
        await Put(rider, "source", "reports/a", "a");
        await Put(rider, "source", "other", "other");
        var snapshot = (await rider.OpenReadAsync("source", "reports/b")).Value;
        await using var snapshotContent = snapshot.Content;
        await Put(rider, "source", "reports/b", "new content");
        var copied = await rider.CopyAsync("source", "reports/b", "archive", "copy");
        Assert.True(copied.HasValue);
        Assert.Equal("archive", copied.Value.BucketName);
        Assert.Equal("copy", copied.Value.ObjectKey);
        Assert.Equal("new content", await Read(rider, "archive", "copy"));
        Assert.True((await rider.CopyAsync("archive", "copy", "archive", "copy")).HasValue);
        var keys = new List<string>();
        await foreach (var info in rider.ListAsync("source", "reports/")) keys.Add(info.ObjectKey);
        Assert.Equal(["reports/a", "reports/b"], keys);
        Assert.True(await rider.DeleteAsync("source", "reports/b"));
        Assert.False(await rider.DeleteAsync("source", "reports/b"));
        Assert.False(await rider.ExistsAsync("source", "reports/b"));
        Assert.False((await rider.GetInfoAsync("source", "reports/b")).HasValue);
        Assert.False((await rider.CopyAsync("source", "missing", "archive", "missing")).HasValue);
        using var reader = new StreamReader(snapshotContent);
        Assert.Equal("old", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task ObjectNames_ShouldBeOpaqueAndCaseSensitive()
    {
        var rider = CreateRider();
        string[] keys = ["../outside", "C:\\outside", "/absolute", "a", "A", "a/b", "a\\b", "中文", "file.obj", "file.tmp"];
        foreach (var key in keys) await Put(rider, "../bucket", key, key);
        foreach (var key in keys) Assert.Equal(key, await Read(CreateRider(), "../bucket", key));
        Assert.False(await rider.ExistsAsync("../BUCKET", "a"));
        Assert.Equal(keys.Length, Directory.GetFiles(_root, "*.obj", SearchOption.AllDirectories).Length);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task InterruptedWrite_ShouldPreservePreviousObjectAndCleanTemporaryFiles()
    {
        var rider = CreateRider();
        await Put(rider, "bucket", "key", "original");
        using var failing = new FailingStream();
        await Assert.ThrowsAsync<IOException>(async () => await rider.PutAsync("bucket", "key", failing));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var input = new MemoryStream("new"u8.ToArray());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await rider.PutAsync("bucket", "key", input, cancellationToken: cancelled.Token));
        Assert.Equal("original", await Read(rider, "bucket", "key"));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task TestHosts_ShouldUseIsolatedMemoryAndShareStateBetweenScopes()
    {
        var builder = NOFTestAppBuilder.Create();
        using var provider = builder.Services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var rider = Assert.IsType<MemoryObjectStorageRider>(first.ServiceProvider.GetRequiredService<IObjectStorageRider>());
        await Put(rider, "bucket", "key", "value");
        Assert.True(await second.ServiceProvider.GetRequiredService<IObjectStorageRider>().ExistsAsync("bucket", "key"));
        using var other = NOFTestAppBuilder.Create().Services.BuildServiceProvider();
        using var otherScope = other.CreateScope();
        Assert.False(await otherScope.ServiceProvider.GetRequiredService<IObjectStorageRider>().ExistsAsync("bucket", "key"));
        var services = new ServiceCollection();
        services.AddMemoryObjectStorage().AddFileSystemObjectStorage(options => options.RootPath = _root);
        using var fileProvider = services.BuildServiceProvider();
        using var fileScope = fileProvider.CreateScope();
        Assert.IsType<FileSystemObjectStorageRider>(fileScope.ServiceProvider.GetRequiredService<IObjectStorageRider>());
    }

    private static async Task Put(IObjectStorageRider rider, string bucket, string key, string value)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(value));
        await rider.PutAsync(bucket, key, input);
    }

    private static async Task<string> Read(IObjectStorageRider rider, string bucket, string key)
    {
        var result = (await rider.OpenReadAsync(bucket, key)).Value;
        await using var content = result.Content;
        using var reader = new StreamReader(content);
        return await reader.ReadToEndAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class FailingStream : MemoryStream
    {
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await destination.WriteAsync("partial"u8.ToArray(), cancellationToken);
            throw new IOException("Simulated interrupted upload.");
        }
    }
}
