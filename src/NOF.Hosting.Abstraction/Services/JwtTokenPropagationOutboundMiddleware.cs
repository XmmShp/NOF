using NOF.Abstraction;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace NOF.Hosting;

/// <summary>Propagates JWT tokens to outbound RPC requests.</summary>
public sealed class JwtTokenPropagationOutboundMiddleware : IRequestOutboundMiddleware
{
    public TopologyComparison Compare(IRequestOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

    private readonly IUserContext _userContext;
    private readonly ILogger<JwtTokenPropagationOutboundMiddleware> _logger;

    public JwtTokenPropagationOutboundMiddleware(IUserContext userContext, ILogger<JwtTokenPropagationOutboundMiddleware> logger)
    {
        _userContext = userContext;
        _logger = logger;
    }

    public ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        Propagate(context.Headers);
        return next(context, request, cancellationToken);
    }

    private void Propagate(IDictionary<string, string?> headers)
    {
        foreach (var identity in _userContext.User.GetIdentities<JwtClaimsIdentity>()
            .Where(identity => identity.DownstreamPropagation is not null)
            .Where(identity => identity.Token.Length > 0))
        {
            var propagation = identity.DownstreamPropagation!;
            WriteHeader(headers, propagation.HeaderName, FormatHeaderValue(propagation, identity.Token));
        }
    }

    private static string FormatHeaderValue(JwtPropagation propagation, string token)
        => string.IsNullOrEmpty(propagation.TokenType)
            ? token
            : $"{propagation.TokenType} {token}";

    private void WriteHeader(IDictionary<string, string?> headers, string headerName, string headerValue)
    {
        if (headers.TryGetValue(headerName, out var existingValue)
            && !string.IsNullOrWhiteSpace(existingValue)
            && !string.Equals(existingValue, headerValue, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "JWT propagation outbound middleware is overwriting existing header '{HeaderName}'. Check outbound middleware ordering and token propagation configuration.",
                headerName);
        }

        headers[headerName] = headerValue;
    }
}
