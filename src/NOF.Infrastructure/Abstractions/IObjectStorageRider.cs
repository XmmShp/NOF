using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure;

/// <summary>
/// Defines the provider extension point used by <see cref="IObjectStorage"/>.
/// Rider implementations operate on physical object keys after framework prefixes have been applied.
/// </summary>
public interface IObjectStorageRider
{
    /// <summary>Gets whether this rider supports presigned upload and download requests.</summary>
    bool SupportsPresignedRequests => false;

    /// <summary>Signs a direct upload request using a physical object key.</summary>
    ValueTask<ObjectStoragePresignedRequest> CreatePresignedUploadAsync(
        string bucketName, string objectKey, TimeSpan lifetime,
        ObjectStorageWriteOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This object storage rider does not support presigned requests.");

    /// <summary>Signs a direct download request using a physical object key.</summary>
    ValueTask<ObjectStoragePresignedRequest> CreatePresignedDownloadAsync(
        string bucketName, string objectKey, TimeSpan lifetime, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This object storage rider does not support presigned requests.");

    ValueTask<ObjectStorageObjectInfo> PutAsync(
        string bucketName,
        string objectKey,
        Stream content,
        ObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<Optional<ObjectStorageReadResult>> OpenReadAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);

    ValueTask<Optional<ObjectStorageObjectInfo>> GetInfoAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);

    ValueTask<bool> ExistsAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);

    ValueTask<bool> DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);

    ValueTask<Optional<ObjectStorageObjectInfo>> CopyAsync(
        string sourceBucketName,
        string sourceObjectKey,
        string destinationBucketName,
        string destinationObjectKey,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ObjectStorageObjectInfo> ListAsync(
        string bucketName,
        string? prefix = null,
        CancellationToken cancellationToken = default);
}
