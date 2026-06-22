using NOF.Abstraction;

namespace NOF.Hosting;

/// <summary>
/// Downstream propagation settings for one JWT identity.
/// </summary>
public sealed class JwtPropagation
{
    /// <summary>
    /// The header name used to propagate the token downstream.
    /// </summary>
    public string HeaderName { get; set; } = NOFAbstractionConstants.Transport.Headers.Authorization;

    /// <summary>
    /// The token type prefix used to propagate the token downstream.
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Gets or sets whether downstream calls should use OAuth token exchange instead of directly forwarding the token.
    /// Default is false.
    /// </summary>
    public bool EnableTokenExchange { get; set; }
}
