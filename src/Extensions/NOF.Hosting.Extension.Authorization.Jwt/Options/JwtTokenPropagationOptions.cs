using NOF.Contract;

namespace NOF.Hosting.Extension.Authorization.Jwt;

/// <summary>
/// Configuration options for outbound JWT token propagation.
/// </summary>
public class JwtTokenPropagationOptions
{
    /// <summary>
    /// Gets or sets the header name used to propagate the authorization token.
    /// Default is "Authorization".
    /// </summary>
    public string HeaderName { get; set; } = NOFContractConstants.Transport.Headers.Authorization;

    /// <summary>
    /// Gets or sets the token type prefix (e.g., "Bearer").
    /// Default is "Bearer".
    /// </summary>
    public string TokenType { get; set; } = "Bearer";
}
