using NOF.Abstraction;

namespace NOF.Hosting;

/// <summary>Propagates JWT tokens to outbound RPC requests.</summary>
public sealed class JwtTokenPropagationOutboundMiddleware : IRequestOutboundMiddleware
{
    public TopologyComparison Compare(IRequestOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

    private readonly IUserContext _userContext;

    public JwtTokenPropagationOutboundMiddleware(IUserContext userContext)
    {
        _userContext = userContext;
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
            headers[propagation.HeaderName] = FormatHeaderValue(propagation, identity.Token);
        }
    }

    private static string FormatHeaderValue(JwtPropagation propagation, string token)
        => string.IsNullOrEmpty(propagation.TokenType)
            ? token
            : $"{propagation.TokenType} {token}";
}
