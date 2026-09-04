using System.Collections.ObjectModel;

namespace NOF.Application;

/// <summary>
/// Describes a stored object and its content metadata.
/// </summary>
public sealed record ObjectStorageObjectInfo
{
    /// <summary>
    /// Initializes a new object metadata value.
    /// </summary>
    public ObjectStorageObjectInfo(
        string bucketName,
        string objectKey,
        long contentLength,
        DateTimeOffset lastModified,
        string? entityTag = null,
        string? contentType = null,
        string? contentEncoding = null,
        string? cacheControl = null,
        string? contentDisposition = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentOutOfRangeException.ThrowIfNegative(contentLength);

        BucketName = bucketName;
        ObjectKey = objectKey;
        ContentLength = contentLength;
        LastModified = lastModified;
        EntityTag = entityTag;
        ContentType = contentType;
        ContentEncoding = contentEncoding;
        CacheControl = cacheControl;
        ContentDisposition = contentDisposition;
        Metadata = CopyMetadata(metadata);
    }

    /// <summary>Gets the bucket name.</summary>
    public string BucketName { get; init; }

    /// <summary>Gets the object key.</summary>
    public string ObjectKey { get; init; }

    /// <summary>Gets the content length in bytes.</summary>
    public long ContentLength { get; init; }

    /// <summary>Gets the provider-reported last-modified timestamp.</summary>
    public DateTimeOffset LastModified { get; init; }

    /// <summary>Gets the provider entity tag, when available.</summary>
    public string? EntityTag { get; init; }

    /// <summary>Gets the media type, when available.</summary>
    public string? ContentType { get; init; }

    /// <summary>Gets the content encoding, when available.</summary>
    public string? ContentEncoding { get; init; }

    /// <summary>Gets the cache-control value, when available.</summary>
    public string? CacheControl { get; init; }

    /// <summary>Gets the content-disposition value, when available.</summary>
    public string? ContentDisposition { get; init; }

    /// <summary>Gets provider-neutral custom metadata.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }

    private static IReadOnlyDictionary<string, string> CopyMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return ReadOnlyDictionary<string, string>.Empty;
        }

        return new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase));
    }
}
