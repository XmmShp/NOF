namespace NOF.Infrastructure.Core;

/// <summary>
/// Configuration options for authorization token handling (inbound parsing and outbound propagation).
/// </summary>
public class AuthorizationOptions
{
    /// <summary>
    /// Gets or sets the header name used to propagate the authorization token.
    /// Default is "Authorization".
    /// </summary>
    public string HeaderName { get; set; } = NOFInfrastructureCoreConstants.Transport.Headers.Authorization;

    /// <summary>
    /// Gets or sets the token type prefix (e.g., "Bearer").
    /// Default is "Bearer".
    /// </summary>
    public string TokenType { get; set; } = "Bearer";
}
