using NOF.Abstraction;

namespace NOF.Hosting.AspNetCore;

/// <summary>
/// Configuration options for propagating selected inbound HTTP headers to outbound RPC requests.
/// </summary>
public class HttpHeaderOutboundOptions
{
    /// <summary>
    /// Gets the inbound HTTP header name patterns that can be propagated to outbound requests.
    /// The asterisk character matches any sequence of characters.
    /// </summary>
    public List<string> AllowedHeaders { get; set; } =
    [
        NOFAbstractionConstants.Transport.Headers.Authorization,
        "NOF.*",
        "X-*"
    ];
}
