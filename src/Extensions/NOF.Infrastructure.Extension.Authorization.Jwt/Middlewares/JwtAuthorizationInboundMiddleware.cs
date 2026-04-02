using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Contract;
using System.IdentityModel.Tokens.Jwt;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>Resolves the current user from a JWT before tenant resolution runs.</summary>
public class JwtAuthorizationInboundMiddlewareStep : IInboundMiddlewareStep<JwtAuthorizationInboundMiddlewareStep, JwtAuthorizationInboundMiddleware>,
    IAfter<ExceptionInboundMiddlewareStep>,
    IBefore<TenantInboundMiddlewareStep>;

/// <summary>
/// Inbound middleware that extracts and validates a JWT from inbound headers,
/// then populates the current user context.
/// </summary>
public sealed class JwtAuthorizationInboundMiddleware : IInboundMiddleware
{
    private readonly IUserContext _userContext;
    private readonly IJwksProvider _jwksProvider;
    private readonly JwtAuthorizationOptions _jwtOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ILogger<JwtAuthorizationInboundMiddleware> _logger;

    public JwtAuthorizationInboundMiddleware(
        IUserContext userContext,
        IJwksProvider jwksProvider,
        IOptions<JwtAuthorizationOptions> jwtOptions,
        ILogger<JwtAuthorizationInboundMiddleware> logger)
    {
        _userContext = userContext;
        _jwksProvider = jwksProvider;
        _jwtOptions = jwtOptions.Value;
        _tokenHandler = new JwtSecurityTokenHandler();
        _logger = logger;
    }

    public async ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken)
    {
        if (!context.ExecutionContext.TryGetValue(_jwtOptions.HeaderName, out var authHeader) ||
            string.IsNullOrEmpty(authHeader))
        {
            _userContext.UnsetUser();
            await next(cancellationToken);
            return;
        }

        var prefix = _jwtOptions.TokenType + " ";
        var token = authHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authHeader[prefix.Length..]
            : authHeader;

        if (string.IsNullOrEmpty(token))
        {
            _userContext.UnsetUser();
            await next(cancellationToken);
            return;
        }

        try
        {
            var keys = await _jwksProvider.GetSecurityKeysAsync(cancellationToken);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = !string.IsNullOrEmpty(_jwtOptions.Issuer),
                ValidIssuer = _jwtOptions.Issuer,
                ValidateAudience = !string.IsNullOrEmpty(_jwtOptions.Audience),
                ValidAudience = _jwtOptions.Audience,
                ValidateLifetime = true,
                IssuerSigningKeys = keys,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);
            _userContext.SetUser(new JwtClaimsPrincipal(principal, token));
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogDebug("JWT token expired");
            _userContext.UnsetUser();
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogDebug(ex, "JWT token validation failed: {Message}", ex.Message);
            _userContext.UnsetUser();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error during JWT token validation");
            _userContext.UnsetUser();
        }

        await next(cancellationToken);
    }
}
