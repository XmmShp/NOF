using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Abstraction;
using NOF.Application;
using NOF.Hosting;
using NOF.Hosting.Extension.Authorization.Jwt;
using System.IdentityModel.Tokens.Jwt;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed class JwtResourceServerInboundMiddleware : RequestInboundMiddleware,
    IAfter<ExceptionInboundMiddleware>,
    IBefore<TenantInboundMiddleware>
{
    private readonly IUserContext _userContext;
    private readonly IJwksProvider _jwksProvider;
    private readonly JwtResourceServerOptions _jwtOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ILogger<JwtResourceServerInboundMiddleware> _logger;
    private readonly IExecutionContext _executionContext;

    public JwtResourceServerInboundMiddleware(
        IUserContext userContext,
        IJwksProvider jwksProvider,
        IOptions<JwtResourceServerOptions> jwtOptions,
        ILogger<JwtResourceServerInboundMiddleware> logger,
        IExecutionContext executionContext)
    {
        _userContext = userContext;
        _jwksProvider = jwksProvider;
        _jwtOptions = jwtOptions.Value;
        _tokenHandler = new JwtSecurityTokenHandler();
        _logger = logger;
        _executionContext = executionContext;
    }

    public override async ValueTask InvokeAsync(RequestInboundContext context, InboundDelegate<RequestInboundContext> next, CancellationToken cancellationToken)
    {
        if (!_executionContext.TryGetValue(_jwtOptions.HeaderName, out var authHeader) || string.IsNullOrEmpty(authHeader))
        {
            await next(cancellationToken);
            return;
        }

        var prefix = _jwtOptions.TokenType + " ";
        var token = authHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authHeader[prefix.Length..]
            : authHeader;

        if (string.IsNullOrEmpty(token))
        {
            await next(cancellationToken);
            return;
        }

        try
        {
            var keys = await _jwksProvider.GetSecurityKeysAsync(cancellationToken);
            if (keys.Count == 0)
            {
                _logger.LogDebug("No JWKS keys available for JWT validation");
                await next(cancellationToken);
                return;
            }

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
            _userContext.User = new JwtClaimsPrincipal(principal, token);
            _executionContext.Remove(_jwtOptions.HeaderName);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogDebug("JWT token expired");
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogDebug(ex, "JWT token validation failed: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error during JWT token validation");
        }

        await next(cancellationToken);
    }
}
