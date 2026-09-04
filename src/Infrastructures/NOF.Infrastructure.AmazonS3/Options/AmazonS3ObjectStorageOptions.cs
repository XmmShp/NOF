namespace NOF.Infrastructure.AmazonS3;

/// <summary>
/// Configures the AWS S3 client and S3-compatible upload behavior.
/// </summary>
public sealed class AmazonS3ObjectStorageOptions
{
    /// <summary>
    /// Gets or sets the AWS region system name used for endpoint selection and request signing.
    /// </summary>
    /// <example><c>ap-southeast-1</c></example>
    public string? Region { get; set; }

    /// <summary>
    /// Gets or sets a custom S3-compatible service URL. Leave unset when using AWS endpoints.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Gets or sets whether requests use path-style bucket addressing.
    /// This is commonly required by local and S3-compatible services.
    /// </summary>
    public bool ForcePathStyle { get; set; }

    /// <summary>
    /// Gets or sets an explicit access key ID. When omitted, the AWS SDK credential chain is used.
    /// </summary>
    public string? AccessKeyId { get; set; }

    /// <summary>
    /// Gets or sets an explicit secret access key.
    /// </summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>
    /// Gets or sets an optional session token for temporary credentials.
    /// </summary>
    public string? SessionToken { get; set; }

    /// <summary>
    /// Gets or sets whether uploads use AWS chunked transfer encoding.
    /// Disable this when the configured S3-compatible service does not support it.
    /// </summary>
    public bool UseChunkEncoding { get; set; } = true;
}
