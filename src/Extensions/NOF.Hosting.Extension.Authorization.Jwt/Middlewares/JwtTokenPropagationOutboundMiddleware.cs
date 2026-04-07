using Microsoft.Extensions.Options;
using NOF.Contract;
using NOF.Hosting;

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
    private readonly IExecutionContext _executionContext;

    public JwtTokenPropagationOutboundMiddleware(
        IUserContext userContext,
        IOptions<JwtTokenPropagationOptions> options,
        IExecutionContext executionContext)
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

