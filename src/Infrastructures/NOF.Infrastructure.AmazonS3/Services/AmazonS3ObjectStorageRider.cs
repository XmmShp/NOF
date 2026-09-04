using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;

namespace NOF.Infrastructure.AmazonS3;

/// <summary>
/// Implements NOF object storage over AWS S3 or an S3-compatible endpoint.
/// </summary>
public sealed class AmazonS3ObjectStorageRider : IObjectStorageRider
{
    private const string MetadataHeaderPrefix = "x-amz-meta-";

    private readonly IAmazonS3 _client;
    private readonly AmazonS3ObjectStorageOptions _options;

    /// <summary>
    /// Initializes a new AWS S3 object storage rider.
    /// </summary>
    public AmazonS3ObjectStorageRider(
        IAmazonS3 client,
        IOptions<AmazonS3ObjectStorageOptions> options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async ValueTask<ObjectStorageObjectInfo> PutAsync(
        string bucketName,
        string objectKey,
        Stream content,
        ObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);
        ValidateContent(content);

        var contentLength = TryGetRemainingLength(content);
        CountingReadStream? countingStream = null;
        if (contentLength is null)
        {
            countingStream = new CountingReadStream(content);
        }

        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            InputStream = countingStream ?? content,
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
            UseChunkEncoding = _options.UseChunkEncoding,
            ContentType = options?.ContentType
        };
        ApplyWriteOptions(request, options);

        var response = await _client.PutObjectAsync(request, cancellationToken);
        return new ObjectStorageObjectInfo(
            bucketName,
            objectKey,
            contentLength ?? countingStream!.BytesRead,
            DateTimeOffset.UtcNow,
            response.ETag,
            options?.ContentType,
            options?.ContentEncoding,
            options?.CacheControl,
            options?.ContentDisposition,
            options?.Metadata);
    }

    /// <inheritdoc />
    public async ValueTask<Optional<ObjectStorageReadResult>> OpenReadAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);

        GetObjectResponse response;
        try
        {
            response = await _client.GetObjectAsync(bucketName, objectKey, cancellationToken);
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            return Optional.None;
        }

        try
        {
            var info = ToObjectInfo(bucketName, objectKey, response);
            return Optional.Of(new ObjectStorageReadResult(
                new ResponseOwningStream(response),
                info));
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<Optional<ObjectStorageObjectInfo>> GetInfoAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);

        try
        {
            var response = await _client.GetObjectMetadataAsync(
                bucketName,
                objectKey,
                cancellationToken);
            return Optional.Of(ToObjectInfo(bucketName, objectKey, response));
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            return Optional.None;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> ExistsAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
        => (await GetInfoAsync(bucketName, objectKey, cancellationToken)).HasValue;

    /// <inheritdoc />
    public async ValueTask<bool> DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);
        if (!await ExistsAsync(bucketName, objectKey, cancellationToken))
        {
            return false;
        }

        try
        {
            await _client.DeleteObjectAsync(bucketName, objectKey, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<Optional<ObjectStorageObjectInfo>> CopyAsync(
        string sourceBucketName,
        string sourceObjectKey,
        string destinationBucketName,
        string destinationObjectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(sourceBucketName, sourceObjectKey);
        ValidateLocation(destinationBucketName, destinationObjectKey);

        var sourceInfo = await GetInfoAsync(
            sourceBucketName,
            sourceObjectKey,
            cancellationToken);
        if (!sourceInfo.HasValue)
        {
            return Optional.None;
        }

        try
        {
            var response = await _client.CopyObjectAsync(
                new CopyObjectRequest
                {
                    SourceBucket = sourceBucketName,
                    SourceKey = sourceObjectKey,
                    DestinationBucket = destinationBucketName,
                    DestinationKey = destinationObjectKey
                },
                cancellationToken);
            var source = sourceInfo.Value;
            return Optional.Of(new ObjectStorageObjectInfo(
                destinationBucketName,
                destinationObjectKey,
                source.ContentLength,
                ToDateTimeOffset(response.LastModified),
                response.ETag,
                source.ContentType,
                source.ContentEncoding,
                source.CacheControl,
                source.ContentDisposition,
                source.Metadata));
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            return Optional.None;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ObjectStorageObjectInfo> ListAsync(
        string bucketName,
        string? prefix = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);

        var request = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix
        };

        do
        {
            var response = await _client.ListObjectsV2Async(request, cancellationToken);
            foreach (var item in response.S3Objects ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new ObjectStorageObjectInfo(
                    bucketName,
                    item.Key,
                    item.Size ?? 0,
                    ToDateTimeOffset(item.LastModified),
                    item.ETag);
            }

            request.ContinuationToken = response.NextContinuationToken;
            if (response.IsTruncated != true)
            {
                break;
            }
        }
        while (!string.IsNullOrEmpty(request.ContinuationToken));
    }

    private static void ApplyWriteOptions(
        PutObjectRequest request,
        ObjectStorageWriteOptions? options)
    {
        if (options is null)
        {
            return;
        }

        request.Headers.ContentEncoding = options.ContentEncoding;
        request.Headers.CacheControl = options.CacheControl;
        request.Headers.ContentDisposition = options.ContentDisposition;

        if (options.Metadata is null || options.Metadata.Count == 0)
        {
            return;
        }

        foreach (var (key, value) in options.Metadata)
        {
            request.Metadata.Add(key, value);
        }
    }

    private static ObjectStorageObjectInfo ToObjectInfo(
        string bucketName,
        string objectKey,
        GetObjectResponse response)
        => new(
            bucketName,
            objectKey,
            response.Headers.ContentLength,
            ToDateTimeOffset(response.LastModified),
            response.ETag,
            response.Headers.ContentType,
            response.Headers.ContentEncoding,
            response.Headers.CacheControl,
            response.Headers.ContentDisposition,
            CopyMetadata(response.Metadata));

    private static ObjectStorageObjectInfo ToObjectInfo(
        string bucketName,
        string objectKey,
        GetObjectMetadataResponse response)
        => new(
            bucketName,
            objectKey,
            response.ContentLength,
            ToDateTimeOffset(response.LastModified),
            response.ETag,
            response.ContentType,
            response.ContentEncoding,
            response.CacheControl,
            response.ContentDisposition,
            CopyMetadata(response.Metadata));

    private static IReadOnlyDictionary<string, string> CopyMetadata(
        MetadataCollection? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var result = new Dictionary<string, string>(
            metadata.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var key in metadata.Keys)
        {
            var logicalKey = key.StartsWith(MetadataHeaderPrefix, StringComparison.OrdinalIgnoreCase)
                ? key[MetadataHeaderPrefix.Length..]
                : key;
            result[logicalKey] = metadata[key];
        }

        return result;
    }

    private static long? TryGetRemainingLength(Stream content)
    {
        if (!content.CanSeek)
        {
            return null;
        }

        try
        {
            return Math.Max(0, content.Length - content.Position);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime? value)
        => value is null
            ? DateTimeOffset.MinValue
            : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private static DateTimeOffset ToDateTimeOffset(string? value)
        => DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;

    private static bool IsNotFound(AmazonS3Exception exception)
        => exception.StatusCode == HttpStatusCode.NotFound
            || string.Equals(exception.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase)
            || string.Equals(exception.ErrorCode, "NotFound", StringComparison.OrdinalIgnoreCase);

    private static void ValidateLocation(string bucketName, string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
    }

    private static void ValidateContent(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
        {
            throw new ArgumentException("The object content stream must be readable.", nameof(content));
        }
    }
}
