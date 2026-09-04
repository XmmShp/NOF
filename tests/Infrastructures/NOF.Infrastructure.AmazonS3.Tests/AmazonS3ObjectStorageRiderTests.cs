using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NOF.Application;
using NOF.Hosting;
using NOF.Infrastructure;
using NOF.Infrastructure.AmazonS3;
using System.Net;
using System.Text;
using Xunit;

namespace NOF.Infrastructure.AmazonS3.Tests;

public sealed class AmazonS3ObjectStorageRiderTests
{
    [Fact]
    public async Task PutAsync_ShouldMapWriteOptionsAndPreserveInputStreamOwnership()
    {
        PutObjectRequest? capturedRequest = null;
        var client = new Mock<IAmazonS3>();
        client
            .Setup(service => service.PutObjectAsync(
                It.IsAny<PutObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PutObjectResponse { ETag = "\"etag-42\"" });
        var rider = CreateRider(client.Object, useChunkEncoding: false);
        await using var content = new MemoryStream("content"u8.ToArray());

        var result = await rider.PutAsync(
            "documents",
            "reports/42.txt",
            content,
            new ObjectStorageWriteOptions
            {
                ContentType = "text/plain",
                ContentEncoding = "utf-8",
                CacheControl = "private, max-age=60",
                ContentDisposition = "attachment; filename=42.txt",
                Metadata = new Dictionary<string, string> { ["document-id"] = "42" }
            });

        Assert.NotNull(capturedRequest);
        Assert.Equal("documents", capturedRequest.BucketName);
        Assert.Equal("reports/42.txt", capturedRequest.Key);
        Assert.Same(content, capturedRequest.InputStream);
        Assert.False(capturedRequest.AutoCloseStream);
        Assert.False(capturedRequest.AutoResetStreamPosition);
        Assert.False(capturedRequest.UseChunkEncoding);
        Assert.Equal("text/plain", capturedRequest.ContentType);
        Assert.Equal("utf-8", capturedRequest.Headers.ContentEncoding);
        Assert.Equal("private, max-age=60", capturedRequest.Headers.CacheControl);
        Assert.Equal("attachment; filename=42.txt", capturedRequest.Headers.ContentDisposition);
        Assert.Equal("42", capturedRequest.Metadata["document-id"]);
        Assert.Equal(content.Length, result.ContentLength);
        Assert.Equal("\"etag-42\"", result.EntityTag);
        Assert.True(content.CanRead);
    }

    [Fact]
    public async Task PutAsync_ShouldCountNonSeekableContentWithoutOwningIt()
    {
        var client = new Mock<IAmazonS3>();
        client
            .Setup(service => service.PutObjectAsync(
                It.IsAny<PutObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<PutObjectRequest, CancellationToken>(async (request, cancellationToken) =>
            {
                await request.InputStream.CopyToAsync(Stream.Null, cancellationToken);
                request.InputStream.Dispose();
                return new PutObjectResponse { ETag = "etag" };
            });
        var rider = CreateRider(client.Object);
        await using var content = new NonSeekableReadStream("non-seekable"u8.ToArray());

        var result = await rider.PutAsync("documents", "content.bin", content);

        Assert.Equal(12, result.ContentLength);
        Assert.True(content.CanRead);
    }

    [Fact]
    public async Task OpenReadAsync_ShouldMapResponseAndDisposeTheOwningResponse()
    {
        var responseStream = new TrackingMemoryStream("payload"u8.ToArray());
        var response = new GetObjectResponse
        {
            ResponseStream = responseStream,
            ETag = "etag",
            LastModified = new DateTime(2026, 9, 4, 1, 2, 3, DateTimeKind.Utc)
        };
        response.Headers.ContentLength = 7;
        response.Headers.ContentType = "text/plain";
        response.Headers.ContentEncoding = "utf-8";
        response.Headers.CacheControl = "no-cache";
        response.Headers.ContentDisposition = "inline";
        response.Metadata.Add("document-id", "42");

        var client = new Mock<IAmazonS3>();
        client
            .Setup(service => service.GetObjectAsync(
                "documents",
                "payload.txt",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        var rider = CreateRider(client.Object);

        var result = await rider.OpenReadAsync("documents", "payload.txt");

        Assert.True(result.HasValue);
        Assert.Equal(7, result.Value.ObjectInfo.ContentLength);
        Assert.Equal("text/plain", result.Value.ObjectInfo.ContentType);
        Assert.Equal("utf-8", result.Value.ObjectInfo.ContentEncoding);
        Assert.Equal("no-cache", result.Value.ObjectInfo.CacheControl);
        Assert.Equal("inline", result.Value.ObjectInfo.ContentDisposition);
        Assert.Equal("42", result.Value.ObjectInfo.Metadata["document-id"]);
        using (var reader = new StreamReader(result.Value.Content, Encoding.UTF8, leaveOpen: true))
        {
            Assert.Equal("payload", await reader.ReadToEndAsync());
        }

        await result.Value.Content.DisposeAsync();
        Assert.True(responseStream.IsDisposed);
    }

    [Fact]
    public async Task MissingObjects_ShouldReturnEmptyOptionals()
    {
        var notFound = new AmazonS3Exception("missing")
        {
            StatusCode = HttpStatusCode.NotFound,
            ErrorCode = "NoSuchKey"
        };
        var client = new Mock<IAmazonS3>();
        client
            .Setup(service => service.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(notFound);
        client
            .Setup(service => service.GetObjectMetadataAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(notFound);
        var rider = CreateRider(client.Object);

        var read = await rider.OpenReadAsync("documents", "missing.txt");
        var info = await rider.GetInfoAsync("documents", "missing.txt");
        var exists = await rider.ExistsAsync("documents", "missing.txt");
        var deleted = await rider.DeleteAsync("documents", "missing.txt");

        Assert.False(read.HasValue);
        Assert.False(info.HasValue);
        Assert.False(exists);
        Assert.False(deleted);
        client.Verify(
            service => service.DeleteObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteAnExistingObject()
    {
        var client = new Mock<IAmazonS3>();
        client
            .Setup(service => service.GetObjectMetadataAsync(
                "documents",
                "existing.txt",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse
            {
                ContentLength = 7,
                LastModified = DateTime.UtcNow
            });
        client
            .Setup(service => service.DeleteObjectAsync(
                "documents",
                "existing.txt",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());
        var rider = CreateRider(client.Object);

        var deleted = await rider.DeleteAsync("documents", "existing.txt");

        Assert.True(deleted);
        client.Verify(
            service => service.DeleteObjectAsync(
                "documents",
                "existing.txt",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CopyAsync_ShouldPreserveMetadataAndMapDestination()
    {
        var metadataResponse = new GetObjectMetadataResponse
        {
            ContentLength = 128,
            ETag = "source-etag",
            LastModified = new DateTime(2026, 9, 3, 1, 2, 3, DateTimeKind.Utc),
            ContentType = "application/pdf",
            ContentEncoding = "gzip",
            CacheControl = "private",
            ContentDisposition = "attachment"
        };
        metadataResponse.Metadata.Add("document-id", "42");

        CopyObjectRequest? capturedRequest = null;
        var client = new Mock<IAmazonS3>();
        client
            .Setup(service => service.GetObjectMetadataAsync(
                "source",
                "invoice.pdf",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadataResponse);
        client
            .Setup(service => service.CopyObjectAsync(
                It.IsAny<CopyObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<CopyObjectRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new CopyObjectResponse
            {
                ETag = "destination-etag",
                LastModified = "2026-09-04T01:02:03.000Z"
            });
        var rider = CreateRider(client.Object);

        var result = await rider.CopyAsync(
            "source",
            "invoice.pdf",
            "archive",
            "2026/invoice.pdf");

        Assert.True(result.HasValue);
        Assert.NotNull(capturedRequest);
        Assert.Equal("source", capturedRequest.SourceBucket);
        Assert.Equal("invoice.pdf", capturedRequest.SourceKey);
        Assert.Equal("archive", capturedRequest.DestinationBucket);
        Assert.Equal("2026/invoice.pdf", capturedRequest.DestinationKey);
        Assert.Equal(128, result.Value.ContentLength);
        Assert.Equal("application/pdf", result.Value.ContentType);
        Assert.Equal("42", result.Value.Metadata["document-id"]);
        Assert.Equal("destination-etag", result.Value.EntityTag);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 4, 1, 2, 3, TimeSpan.Zero),
            result.Value.LastModified);
    }

    [Fact]
    public async Task ListAsync_ShouldFollowContinuationTokens()
    {
        var firstPage = new ListObjectsV2Response
        {
            IsTruncated = true,
            NextContinuationToken = "next",
            S3Objects =
            [
                new S3Object
                {
                    Key = "reports/one.txt",
                    Size = 3,
                    ETag = "one",
                    LastModified = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc)
                }
            ]
        };
        var secondPage = new ListObjectsV2Response
        {
            IsTruncated = false,
            S3Objects =
            [
                new S3Object
                {
                    Key = "reports/two.txt",
                    Size = 4,
                    ETag = "two",
                    LastModified = new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc)
                }
            ]
        };
        var continuationTokens = new List<string?>();
        var client = new Mock<IAmazonS3>();
        client
            .Setup(service => service.ListObjectsV2Async(
                It.IsAny<ListObjectsV2Request>(),
                It.IsAny<CancellationToken>()))
            .Returns<ListObjectsV2Request, CancellationToken>((request, _) =>
            {
                continuationTokens.Add(request.ContinuationToken);
                return Task.FromResult(request.ContinuationToken is null ? firstPage : secondPage);
            });
        var rider = CreateRider(client.Object);

        var objects = await ReadAllAsync(rider.ListAsync("documents", "reports/"));

        Assert.Equal([null, "next"], continuationTokens);
        Assert.Equal(["reports/one.txt", "reports/two.txt"], objects.Select(static item => item.ObjectKey));
        Assert.Equal([3L, 4L], objects.Select(static item => item.ContentLength));
        client.Verify(
            service => service.ListObjectsV2Async(
                It.Is<ListObjectsV2Request>(request =>
                    request.BucketName == "documents" && request.Prefix == "reports/"),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public void AddAmazonS3ObjectStorage_ShouldReplaceTheDefaultRider()
    {
        var client = new Mock<IAmazonS3>();
        var services = new ServiceCollection();

        services.AddAmazonS3ObjectStorage(
            client.Object,
            options => options.UseChunkEncoding = false);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Same(client.Object, provider.GetRequiredService<IAmazonS3>());
        Assert.IsType<AmazonS3ObjectStorageRider>(
            scope.ServiceProvider.GetRequiredService<IObjectStorageRider>());
        Assert.False(
            provider.GetRequiredService<IOptions<AmazonS3ObjectStorageOptions>>()
                .Value
                .UseChunkEncoding);
    }

    [Fact]
    public void AddAmazonS3ObjectStorage_ShouldBuildAClientForCompatibleEndpoints()
    {
        var services = new ServiceCollection();
        services.AddAmazonS3ObjectStorage(options =>
        {
            options.ServiceUrl = "http://127.0.0.1:9000";
            options.ForcePathStyle = true;
            options.AccessKeyId = "access-key";
            options.SecretAccessKey = "secret-key";
        });

        using var provider = services.BuildServiceProvider();

        Assert.IsType<AmazonS3Client>(provider.GetRequiredService<IAmazonS3>());
    }

    [Fact]
    public void AddAmazonS3ObjectStorage_ShouldRejectIncompleteEndpointConfiguration()
    {
        var services = new ServiceCollection();
        services.AddAmazonS3ObjectStorage(options =>
            options.AccessKeyId = "access-key-without-a-secret");
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            provider.GetRequiredService<IAmazonS3>);

        Assert.Contains(nameof(AmazonS3ObjectStorageOptions.Region), exception.Message);
        Assert.Contains(nameof(AmazonS3ObjectStorageOptions.ServiceUrl), exception.Message);
    }

    private static AmazonS3ObjectStorageRider CreateRider(
        IAmazonS3 client,
        bool useChunkEncoding = true)
        => new(
            client,
            Options.Create(new AmazonS3ObjectStorageOptions
            {
                UseChunkEncoding = useChunkEncoding
            }));

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

    private sealed class TrackingMemoryStream(byte[] buffer) : MemoryStream(buffer)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class NonSeekableReadStream(byte[] buffer) : Stream
    {
        private readonly MemoryStream _inner = new(buffer);

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] target, int offset, int count)
            => _inner.Read(target, offset, count);

        public override int Read(Span<byte> target) => _inner.Read(target);

        public override ValueTask<int> ReadAsync(
            Memory<byte> target,
            CancellationToken cancellationToken = default)
            => _inner.ReadAsync(target, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] source, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
