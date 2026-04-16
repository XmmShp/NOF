using Microsoft.Extensions.Options;
using NOF.Abstraction;

namespace NOF.Hosting.Extension.Authorization.Jwt;

/// <summary>Propagates JWT tokens to outbound RPC requests.</summary>
public sealed class JwtTokenPropagationOutboundMiddleware : IRequestOutboundMiddleware,
    IAfter<MessageIdOutboundMiddleware>
{
    private readonly IUserContext _userContext;
    private readonly JwtTokenPropagationOptions _options;

    public JwtTokenPropagationOutboundMiddleware(
        IUserContext userContext,
        IOptions<JwtTokenPropagationOptions> options)
    {
        _userContext = userContext;
        _options = options.Value;
    }

    public ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        if (_userContext.User is JwtClaimsPrincipal { Token: { Length: > 0 } token })
        {
            context.Headers[_options.HeaderName] = $"{_options.TokenType} {token}";
        }

        return next(cancellationToken);
    }
}
