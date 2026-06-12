using NOF.Abstraction;

namespace NOF.Hosting.Extension.Authentication;

/// <summary>Propagates access tokens to outbound RPC requests.</summary>
public sealed class AccessTokenPropagationOutboundMiddleware : IRequestOutboundMiddleware
{
    public TopologyComparison Compare(IRequestOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

    private readonly IUserContext _userContext;

    public AccessTokenPropagationOutboundMiddleware(IUserContext userContext)
    {
        _userContext = userContext;
    }

    public ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        foreach (var identity in _userContext.User.GetIdentities<JwtClaimsIdentity>()
            .Where(identity => identity.DownstreamPropagation is not null)
            .Where(identity => identity.Token.Length > 0))
        {
            var propagation = identity.DownstreamPropagation!;
            context.Headers[propagation.HeaderName] = $"{propagation.TokenType} {identity.Token}";
        }

        return next(context, request, cancellationToken);
    }
}
