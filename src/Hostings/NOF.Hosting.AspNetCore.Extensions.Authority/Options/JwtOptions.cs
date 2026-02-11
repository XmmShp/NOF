using System.ComponentModel.DataAnnotations;

namespace NOF;

/// <summary>
/// JWT authentication configuration options.
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// Gets or sets the issuer of the JWT tokens (immutable - identifies the auth center).
    /// </summary>
    [Required(ErrorMessage = "Issuer is required.")]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RSA key size in bits. Default is 2048.
    /// </summary>
    [Range(2048, 4096, ErrorMessage = "KeySize must be between 2048 and 4096.")]
    public int KeySize { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the number of previous signing keys to retain for validation after rotation.
    /// Retired keys can still verify existing tokens but will not be used for signing new tokens.
    /// Default is 2.
    /// </summary>
    [Range(0, 10, ErrorMessage = "RetiredKeyRetentionCount must be between 0 and 10.")]
    public int RetiredKeyRetentionCount { get; set; } = 2;

    /// <summary>
    /// Gets or sets the interval between automatic key rotations.
    /// Default is 30 days.
    /// </summary>
    public TimeSpan KeyRotationInterval { get; set; } = TimeSpan.FromDays(30);
}
