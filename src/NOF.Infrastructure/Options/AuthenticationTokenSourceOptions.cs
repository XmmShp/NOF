using NOF.Abstraction;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Configuration for one access token source header accepted by the resource server.
/// </summary>
public class AuthenticationTokenSourceOptions
{
    private string _headerName = NOFAbstractionConstants.Transport.Headers.Authorization;
    private string _tokenType = "Bearer";
    private JwtPropagation? _downstreamPropagation = new()
    {
        HeaderName = NOFAbstractionConstants.Transport.Headers.Authorization,
        TokenType = "Bearer"
    };
    private bool _downstreamPropagationCustomized;

    /// <summary>
    /// Gets or sets the inbound header name used to read the authorization token.
    /// Default is "Authorization".
    /// </summary>
    public string HeaderName
    {
        get => _headerName;
        set
        {
            _headerName = value;
            if (!_downstreamPropagationCustomized && _downstreamPropagation is not null)
            {
                _downstreamPropagation.HeaderName = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the inbound token type prefix (for example, "Bearer").
    /// Default is "Bearer".
    /// </summary>
    public string TokenType
    {
        get => _tokenType;
        set
        {
            _tokenType = value;
            if (!_downstreamPropagationCustomized && _downstreamPropagation is not null)
            {
                _downstreamPropagation.TokenType = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets downstream propagation settings for matched identities.
    /// Null means do not propagate downstream. By default, it follows the inbound
    /// <see cref="HeaderName"/> and <see cref="TokenType"/>.
    /// </summary>
    public JwtPropagation? DownstreamPropagation
    {
        get => _downstreamPropagation;
        set
        {
            _downstreamPropagation = value;
            _downstreamPropagationCustomized = true;
        }
    }
}
