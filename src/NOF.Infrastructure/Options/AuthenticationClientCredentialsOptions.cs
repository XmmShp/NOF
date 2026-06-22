namespace NOF.Infrastructure;

/// <summary>
/// Client credentials used by infrastructure features that need a service access token.
/// </summary>
public sealed class AuthenticationClientCredentialsOptions
{
    /// <summary>
    /// Gets or sets the OAuth client identifier.
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the OAuth client secret.
    /// </summary>
    public required string ClientSecret { get; set; }
}
