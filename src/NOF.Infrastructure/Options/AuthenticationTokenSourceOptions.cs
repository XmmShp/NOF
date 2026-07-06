using NOF.Abstraction;

namespace NOF.Infrastructure;

/// <summary>
/// Configuration for one access token source header accepted by the resource server.
/// </summary>
public class AuthenticationTokenSourceOptions
{

    /// <summary>
    /// Gets or sets the inbound header name used to read the authorization token.
    /// Default is "Authorization".
    /// </summary>
    public string HeaderName { get; set; } = NOFAbstractionConstants.Transport.Headers.Authorization;

    /// <summary>
    /// Gets or sets the inbound token type prefix (for example, "Bearer").
    /// Default is "Bearer".
    /// </summary>
    public string TokenType { get; set; } = "Bearer";
}
