using Microsoft.Extensions.Options;
using NOF.Abstraction;

namespace NOF.Hosting.Extension.Authorization.Jwt;

/// <summary>Propagates JWT tokens to outbound messages.</summary>
/// <summary>
/// Outbound middleware that propagates the current user's JWT token
/// into outbound message headers for inter-service calls.
/// </summary>
public sealed class JwtTokenPropagationOutboundMiddleware : IOutboundMiddleware,
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

    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        if (_userContext.User is JwtClaimsPrincipal { Token: { Length: > 0 } token })
        {
            context.Headers[_options.HeaderName] = $"{_options.TokenType} {token}";
        }

        return next(cancellationToken);
    }
}
