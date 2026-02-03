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
    /// Gets or sets the master security key for deriving client-specific keys.
    /// </summary>
    [Required(ErrorMessage = "MasterSecurityKey is required.")]
    [MinLength(32, ErrorMessage = "MasterSecurityKey must be at least 32 characters long.")]
    public string MasterSecurityKey { get; set; } = string.Empty;
}
