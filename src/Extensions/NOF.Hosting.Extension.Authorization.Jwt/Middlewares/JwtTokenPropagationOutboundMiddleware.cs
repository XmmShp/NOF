using NOF.Abstraction;

namespace NOF.Hosting.Extension.Authorization.Jwt;

/// <summary>Propagates JWT tokens to outbound RPC requests.</summary>
public sealed class JwtTokenPropagationOutboundMiddleware : IRequestOutboundMiddleware
{
    private readonly IUserContext _userContext;

    public JwtTokenPropagationOutboundMiddleware(IUserContext userContext)
    {
        _userContext = userContext;
    }

    public ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        foreach (var identity in _userContext.User.GetIdentities<JwtClaimsIdentity>()
            .Where(identity => identity.DownstreamPropagation is not null)
            .Where(identity => identity.Token.Length > 0))
        {
            var propagation = identity.DownstreamPropagation!;
            context.Headers[propagation.HeaderName] = $"{propagation.TokenType} {identity.Token}";
        }

        return next(cancellationToken);
    }
}
