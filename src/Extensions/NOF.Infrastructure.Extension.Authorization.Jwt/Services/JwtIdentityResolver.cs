using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Infrastructure.Abstraction;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// JWT-based implementation of <see cref="IIdentityResolver"/>.
/// Extracts the Bearer token from the Authorization header, validates it against
/// the configured JWKS keys, and returns a <see cref="JwtClaimsPrincipal"/> that
/// carries the raw token for downstream propagation.
/// </summary>
public sealed class JwtIdentityResolver : IIdentityResolver
{
    private readonly IJwksProvider _jwksProvider;
    private readonly JwtAuthorizationOptions _jwtOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ILogger<JwtIdentityResolver> _logger;

    public JwtIdentityResolver(
        IJwksProvider jwksProvider,
        IOptions<JwtAuthorizationOptions> jwtOptions,
        ILogger<JwtIdentityResolver> logger)
    {
        _jwksProvider = jwksProvider;
        _jwtOptions = jwtOptions.Value;
        _tokenHandler = new JwtSecurityTokenHandler();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ClaimsPrincipal?> ResolveAsync(InboundContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Headers.TryGetValue(_jwtOptions.HeaderName, out var authHeader) ||
            string.IsNullOrEmpty(authHeader))
        {
            return null;
        }

        var prefix = _jwtOptions.TokenType + " ";
        var token = authHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authHeader[prefix.Length..]
            : authHeader;

        if (string.IsNullOrEmpty(token))
        {
            return null;
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
            return new JwtClaimsPrincipal(principal, token);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogDebug("JWT token expired");
            return null;
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogDebug(ex, "JWT token validation failed: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error during JWT token validation");
            return null;
        }
    }
}
