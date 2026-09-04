using Microsoft.Extensions.Options;
using NOF.Application;
using System.Text;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public sealed class ObjectStorageServiceTests
{
    [Fact]
    public async Task MemoryRider_ShouldRoundTripContentAndMetadata()
    {
        using var rider = new MemoryObjectStorageRider();
        var metadata = new Dictionary<string, string> { ["document-id"] = "42" };
        await using var input = new MemoryStream("hello object storage"u8.ToArray());

        var stored = await rider.PutAsync(
            "documents",
            "reports/hello.txt",
            input,
            new ObjectStorageWriteOptions
            {
                ContentType = "text/plain",
                ContentEncoding = "utf-8",
                CacheControl = "private, max-age=60",
                ContentDisposition = "attachment; filename=hello.txt",
                Metadata = metadata
            });
        metadata["document-id"] = "changed";

        Assert.Equal("documents", stored.BucketName);
        Assert.Equal("reports/hello.txt", stored.ObjectKey);
        Assert.Equal(input.Length, stored.ContentLength);
        Assert.False(string.IsNullOrWhiteSpace(stored.EntityTag));
        Assert.Equal("text/plain", stored.ContentType);
        Assert.Equal("utf-8", stored.ContentEncoding);
        Assert.Equal("private, max-age=60", stored.CacheControl);
        Assert.Equal("attachment; filename=hello.txt", stored.ContentDisposition);
        Assert.Equal("42", stored.Metadata["DOCUMENT-ID"]);
        Assert.True(await rider.ExistsAsync("documents", "reports/hello.txt"));

        var read = await rider.OpenReadAsync("documents", "reports/hello.txt");

        Assert.True(read.HasValue);
        await using var objectContent = read.Value.Content;
        using var textReader = new StreamReader(objectContent, Encoding.UTF8);
        Assert.Equal("hello object storage", await textReader.ReadToEndAsync());
        Assert.Equal(stored, read.Value.ObjectInfo);
    }

    [Fact]
    public async Task MemoryRider_ShouldCopyListAndDeleteObjects()
    {
        using var rider = new MemoryObjectStorageRider();
        await PutTextAsync(rider, "source", "reports/one.txt", "one");
        await PutTextAsync(rider, "source", "reports/two.txt", "two");
        await PutTextAsync(rider, "source", "images/logo.png", "logo");

        var copied = await rider.CopyAsync(
            "source",
            "reports/one.txt",
            "archive",
            "2026/one.txt");
        var missingCopy = await rider.CopyAsync(
            "source",
            "missing.txt",
            "archive",
            "missing.txt");
        var reports = await ReadAllAsync(rider.ListAsync("source", "reports/"));

        Assert.True(copied.HasValue);
        Assert.Equal("archive", copied.Value.BucketName);
        Assert.Equal("2026/one.txt", copied.Value.ObjectKey);
        Assert.False(missingCopy.HasValue);
        Assert.Equal(["reports/one.txt", "reports/two.txt"], reports.Select(static item => item.ObjectKey));
        Assert.True(await rider.DeleteAsync("source", "reports/one.txt"));
        Assert.False(await rider.DeleteAsync("source", "reports/one.txt"));
        Assert.False((await rider.GetInfoAsync("source", "reports/one.txt")).HasValue);

        var copiedRead = await rider.OpenReadAsync("archive", "2026/one.txt");
        Assert.True(copiedRead.HasValue);
        await using var copiedContent = copiedRead.Value.Content;
        using var copiedReader = new StreamReader(copiedContent, Encoding.UTF8);
        Assert.Equal("one", await copiedReader.ReadToEndAsync());
    }

    [Fact]
    public async Task Service_ShouldApplyTenantPrefixWithoutLeakingPhysicalKeys()
    {
        using var rider = new MemoryObjectStorageRider();
        var currentTenant = new CurrentTenant();
        var service = new ObjectStorageService(
            rider,
            Options.Create(new ObjectStorageOptions
            {
                KeyPrefix = "tenants/{tenantId}/"
            }),
            currentTenant);

        using (currentTenant.PushTenant("tenanta"))
        {
            await PutTextAsync(service, "documents", "invoices/42.txt", "tenant a");

            Assert.True(await rider.ExistsAsync(
                "documents",
                "tenants/tenanta/invoices/42.txt"));

            var logicalInfo = await service.GetInfoAsync("documents", "invoices/42.txt");
            Assert.True(logicalInfo.HasValue);
            Assert.Equal("invoices/42.txt", logicalInfo.Value.ObjectKey);

            var listed = await ReadAllAsync(service.ListAsync("documents", "invoices/"));
            var item = Assert.Single(listed);
            Assert.Equal("invoices/42.txt", item.ObjectKey);

            var physicalInfo = await service.IgnoreKeyPrefix().GetInfoAsync(
                "documents",
                "tenants/tenanta/invoices/42.txt");
            Assert.True(physicalInfo.HasValue);
            Assert.Equal("tenants/tenanta/invoices/42.txt", physicalInfo.Value.ObjectKey);
        }

        using (currentTenant.PushTenant("tenantb"))
        {
            Assert.False(await service.ExistsAsync("documents", "invoices/42.txt"));
        }
    }

    [Fact]
    public async Task DifferentMemoryStates_ShouldRemainIsolated()
    {
        using var stateA = new MemoryObjectStorageRiderState();
        using var stateB = new MemoryObjectStorageRiderState();
        using var riderA = new MemoryObjectStorageRider(stateA);
        using var riderB = new MemoryObjectStorageRider(stateB);

        await PutTextAsync(riderA, "documents", "shared.txt", "host a");

        Assert.True(await riderA.ExistsAsync("documents", "shared.txt"));
        Assert.False(await riderB.ExistsAsync("documents", "shared.txt"));
    }

    private static async Task PutTextAsync(
        IObjectStorageRider storage,
        string bucketName,
        string objectKey,
        string content)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await storage.PutAsync(bucketName, objectKey, stream);
    }

    private static async Task PutTextAsync(
        IObjectStorage storage,
        string bucketName,
        string objectKey,
        string content)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await storage.PutAsync(bucketName, objectKey, stream);
    }

    private static async Task<List<ObjectStorageObjectInfo>> ReadAllAsync(
        IAsyncEnumerable<ObjectStorageObjectInfo> source)
    {
        var result = new List<ObjectStorageObjectInfo>();
        await foreach (var item in source)
        {
            result.Add(item);
        }

        return result;
    }
}
