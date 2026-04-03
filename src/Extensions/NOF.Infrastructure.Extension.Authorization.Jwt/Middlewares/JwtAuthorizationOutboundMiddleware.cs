using Microsoft.Extensions.Options;
using NOF.Contract;
using NOF.Hosting;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>Propagates JWT authorization token to outbound messages.</summary>
public class JwtAuthorizationOutboundMiddlewareStep : IOutboundMiddlewareStep<JwtAuthorizationOutboundMiddlewareStep, JwtAuthorizationOutboundMiddleware>,
    IAfter<MessageIdOutboundMiddlewareStep>;

/// <summary>
/// Outbound middleware that propagates the current user's JWT token
/// into the outbound message headers for inter-service calls.
/// Uses pattern matching on <see cref="JwtClaimsPrincipal"/> to extract the raw token.
/// The header name and token type are configurable via <see cref="JwtAuthorizationOptions"/>.
/// </summary>
public sealed class JwtAuthorizationOutboundMiddleware : IOutboundMiddleware
{
    private readonly IUserContext _userContext;
    private readonly JwtAuthorizationOptions _options;
    private readonly IExecutionContext _executionContext;

    public JwtAuthorizationOutboundMiddleware(IUserContext userContext, IOptions<JwtAuthorizationOptions> options, IExecutionContext executionContext)
    {
        _userContext = userContext;
        _options = options.Value;
        _executionContext = executionContext;
    }

    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        if (_userContext.User is JwtClaimsPrincipal { Token: { Length: > 0 } token })
        {
            _executionContext[_options.HeaderName] = $"{_options.TokenType} {token}";
        }

        return next(cancellationToken);
    }
}
