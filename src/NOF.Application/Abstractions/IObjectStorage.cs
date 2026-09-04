using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Provides provider-neutral access to object storage.
/// </summary>
public interface IObjectStorage
{
    /// <summary>
    /// Creates a view that bypasses the configured object-key prefix.
    /// </summary>
    /// <returns>An object storage view that uses physical object keys.</returns>
    IObjectStorage IgnoreKeyPrefix();

    /// <summary>
    /// Creates or replaces an object.
    /// </summary>
    /// <param name="bucketName">The bucket that owns the object.</param>
    /// <param name="objectKey">The logical object key.</param>
    /// <param name="content">The readable content stream. The caller retains ownership of the stream.</param>
    /// <param name="options">Optional content headers and custom metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored object's metadata.</returns>
    ValueTask<ObjectStorageObjectInfo> PutAsync(
        string bucketName,
        string objectKey,
        Stream content,
        ObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an object for streaming reads.
    /// </summary>
    /// <param name="bucketName">The bucket that owns the object.</param>
    /// <param name="objectKey">The logical object key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The readable object, or an empty optional when it does not exist.</returns>
    /// <remarks>The caller owns and must dispose the returned content stream.</remarks>
    ValueTask<Optional<ObjectStorageReadResult>> OpenReadAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata for an object without reading its content.
    /// </summary>
    /// <param name="bucketName">The bucket that owns the object.</param>
    /// <param name="objectKey">The logical object key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The object metadata, or an empty optional when it does not exist.</returns>
    ValueTask<Optional<ObjectStorageObjectInfo>> GetInfoAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether an object exists.
    /// </summary>
    ValueTask<bool> ExistsAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an object if it exists.
    /// </summary>
    /// <returns><see langword="true"/> when an object was deleted; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies an object within or between buckets, replacing the destination if it exists.
    /// </summary>
    /// <returns>The destination metadata, or an empty optional when the source does not exist.</returns>
    ValueTask<Optional<ObjectStorageObjectInfo>> CopyAsync(
        string sourceBucketName,
        string sourceObjectKey,
        string destinationBucketName,
        string destinationObjectKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates objects in a bucket whose logical keys begin with the specified prefix.
    /// </summary>
    /// <remarks>The result order is provider-defined.</remarks>
    IAsyncEnumerable<ObjectStorageObjectInfo> ListAsync(
        string bucketName,
        string? prefix = null,
        CancellationToken cancellationToken = default);
}
