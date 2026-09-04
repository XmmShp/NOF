namespace NOF.Application;

/// <summary>
/// Configures content headers and custom metadata for an object write.
/// </summary>
public sealed class ObjectStorageWriteOptions
{
    /// <summary>Gets or sets the media type.</summary>
    public string? ContentType { get; set; }

    /// <summary>Gets or sets the content encoding.</summary>
    public string? ContentEncoding { get; set; }

    /// <summary>Gets or sets the cache-control value.</summary>
    public string? CacheControl { get; set; }

    /// <summary>Gets or sets the content-disposition value.</summary>
    public string? ContentDisposition { get; set; }

    /// <summary>Gets or sets provider-neutral custom metadata.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
}
