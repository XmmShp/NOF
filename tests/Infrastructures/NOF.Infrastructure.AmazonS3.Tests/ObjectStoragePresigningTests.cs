using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Moq;
using NOF.Application;
using NOF.Contract;
using Xunit;

namespace NOF.Infrastructure.AmazonS3.Tests;

public sealed class ObjectStoragePresigningTests
{
    [Fact]
    public async Task Upload_ShouldSignHeadersAndReturnAnImmutableSnapshot()
    {
        GetPreSignedUrlRequest? captured = null;
        var client = new Mock<IAmazonS3>();
        client.Setup(service => service.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
            .Callback<GetPreSignedUrlRequest>(request => captured = request)
            .ReturnsAsync("https://objects.example/upload?signature=test");
        var rider = CreateRider(client.Object);
        var metadata = new Dictionary<string, string> { ["document-id"] = "42" };
        var before = DateTimeOffset.UtcNow;
        var result = await rider.CreatePresignedUploadAsync("documents", "reports/42.txt", TimeSpan.FromMinutes(5),
            new ObjectStorageWriteOptions
            {
                ContentType = "text/plain",
                ContentEncoding = "utf-8",
                CacheControl = "private",
                ContentDisposition = "attachment",
                Metadata = metadata
            });

        Assert.True(rider.SupportsPresignedRequests);
        Assert.NotNull(captured);
        Assert.Equal(Amazon.S3.HttpVerb.PUT, captured.Verb);
        Assert.Equal("documents", captured.BucketName);
        Assert.Equal("reports/42.txt", captured.Key);
        Assert.Equal(result.ExpiresAt.UtcDateTime, captured.Expires);
        Assert.InRange(result.ExpiresAt, before.AddMinutes(5).AddSeconds(-1), DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.Equal("text/plain", captured.ContentType);
        Assert.Equal("utf-8", captured.Headers.ContentEncoding);
        Assert.Equal("private", captured.Headers.CacheControl);
        Assert.Equal("attachment", captured.Headers.ContentDisposition);
        Assert.Equal("42", captured.Metadata["document-id"]);
        Assert.Equal("PUT", result.Method);
        Assert.Equal("text/plain", result.Headers["content-type"]);
        Assert.Equal("utf-8", result.Headers["Content-Encoding"]);
        Assert.Equal("private", result.Headers["Cache-Control"]);
        Assert.Equal("attachment", result.Headers["Content-Disposition"]);
        Assert.Equal("42", result.Headers["x-amz-meta-document-id"]);
        metadata["document-id"] = "changed";
        Assert.Equal("42", result.Headers["x-amz-meta-document-id"]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)result.Headers).Add("x-test", "value"));
        client.Verify(service => service.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Service_ShouldApplyTenantPrefixesAndHonorAdministrativeViewForBothOperations()
    {
        var keys = new List<string>();
        var client = new Mock<IAmazonS3>();
        client.Setup(service => service.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
            .Callback<GetPreSignedUrlRequest>(request => keys.Add(request.Key))
            .ReturnsAsync("https://objects.example/signed");
        IObjectStorage storage = new ObjectStorageService(CreateRider(client.Object),
            Options.Create(new ObjectStorageOptions { KeyPrefix = "tenants/{tenantId}/" }));
        Assert.True(storage.SupportsPresignedRequests);
        using (Context.PushCurrent(Context.Empty.WithTenantId("tenanta")))
        {
            await storage.CreatePresignedUploadAsync("documents", "file", TimeSpan.FromMinutes(5));
            await storage.CreatePresignedDownloadAsync("documents", "file", TimeSpan.FromMinutes(5));
            await storage.IgnoreKeyPrefix().CreatePresignedUploadAsync("documents", "physical", TimeSpan.FromMinutes(5));
            await storage.IgnoreKeyPrefix().CreatePresignedDownloadAsync("documents", "physical", TimeSpan.FromMinutes(5));
        }
        using (Context.PushCurrent(Context.Empty.WithTenantId("tenantb")))
        {
            await storage.CreatePresignedDownloadAsync("documents", "file", TimeSpan.FromMinutes(5));
        }
        Assert.Equal(["tenants/tenanta/file", "tenants/tenanta/file", "physical", "physical", "tenants/tenantb/file"], keys);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(604801)]
    public async Task Signing_ShouldRejectInvalidLifetimeBeforeCallingSdk(double seconds)
    {
        var client = new Mock<IAmazonS3>(MockBehavior.Strict);
        var rider = CreateRider(client.Object);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await rider.CreatePresignedUploadAsync("bucket", "key", TimeSpan.FromSeconds(seconds)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await rider.CreatePresignedDownloadAsync("bucket", "key", TimeSpan.FromSeconds(seconds)));
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Signing_ShouldHonorCancellationBeforeAndDuringCredentialResolution()
    {
        var client = new Mock<IAmazonS3>();
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Setup(service => service.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>())).Returns(pending.Task);
        var rider = CreateRider(client.Object);
        using var cancellation = new CancellationTokenSource();
        var signing = rider.CreatePresignedDownloadAsync("bucket", "key", TimeSpan.FromMinutes(5), cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await signing);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await rider.CreatePresignedUploadAsync("bucket", "key", TimeSpan.FromMinutes(5), cancellationToken: cancellation.Token));
        client.Verify(service => service.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()), Times.Once);
        pending.SetResult("https://objects.example/unused");
    }

    [Theory]
    [InlineData("https")]
    [InlineData("http")]
    public async Task RealSdk_ShouldSignBothOperationsOfflineForCustomEndpoints(string scheme)
    {
        using var client = new AmazonS3Client(new BasicAWSCredentials("test-access-key", "test-secret-key"),
            new AmazonS3Config
            {
                ServiceURL = $"{scheme}://storage.example:9000",
                AuthenticationRegion = "us-east-1",
                ForcePathStyle = true
            });
        var rider = CreateRider(client);
        var key = "tenants/a/中文 + file.txt";
        var upload = await rider.CreatePresignedUploadAsync("documents", key, TimeSpan.FromMinutes(10),
            new ObjectStorageWriteOptions { ContentType = "text/plain", Metadata = new Dictionary<string, string> { ["id"] = "42" } });
        var download = await rider.CreatePresignedDownloadAsync("documents", key, TimeSpan.FromDays(7));
        Assert.Equal("PUT", upload.Method);
        Assert.Equal("GET", download.Method);
        Assert.Empty(download.Headers);
        foreach (var signed in new[] { upload, download })
        {
            var uri = new Uri(signed.Url);
            Assert.Equal(scheme, uri.Scheme);
            Assert.Equal("storage.example", uri.Host);
            Assert.Equal("/documents/" + key, Uri.UnescapeDataString(uri.AbsolutePath));
            var query = ParseQuery(uri);
            Assert.Equal("AWS4-HMAC-SHA256", query["X-Amz-Algorithm"]);
            Assert.NotEmpty(query["X-Amz-Signature"]);
            Assert.InRange(int.Parse(query["X-Amz-Expires"]), 1, 604800);
        }
        var uploadQuery = ParseQuery(new Uri(upload.Url));
        Assert.Contains("content-type", uploadQuery["X-Amz-SignedHeaders"]);
        Assert.Contains("x-amz-meta-id", uploadQuery["X-Amz-SignedHeaders"]);
        Assert.Equal("42", upload.Headers["x-amz-meta-id"]);
    }

    private static Dictionary<string, string> ParseQuery(Uri uri)
        => uri.Query.TrimStart('?').Split('&').Select(part => part.Split('=', 2))
            .ToDictionary(parts => Uri.UnescapeDataString(parts[0]), parts => Uri.UnescapeDataString(parts[1]));

    private static AmazonS3ObjectStorageRider CreateRider(IAmazonS3 client)
        => new(client, Options.Create(new AmazonS3ObjectStorageOptions()));
}
