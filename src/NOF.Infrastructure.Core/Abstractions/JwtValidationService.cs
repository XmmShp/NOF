using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Transport-agnostic JWT validation service.
/// Validates a raw Bearer token string against the configured JWKS keys
/// and returns a <see cref="ManagedUser"/> on success.
/// </summary>
public interface IJwtValidationService
{
    /// <summary>
    /// Validates the given JWT token and returns a <see cref="ManagedUser"/> if valid.
    /// Returns <c>null</c> if the token is invalid or expired.
    /// </summary>
    /// <param name="token">The raw JWT token string (without "Bearer " prefix).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ManagedUser?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IJwtValidationService"/> that uses
/// <see cref="IJwksProvider"/> for key resolution and <see cref="JwtClientOptions"/> for validation parameters.
/// </summary>
public class JwtValidationService : IJwtValidationService
{
    private readonly IJwksProvider _jwksProvider;
    private readonly JwtClientOptions _options;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ILogger<JwtValidationService> _logger;

    public JwtValidationService(
        IJwksProvider jwksProvider,
        IOptions<JwtClientOptions> options,
        ILogger<JwtValidationService> logger)
    {
        _jwksProvider = jwksProvider;
        _options = options.Value;
        _tokenHandler = new JwtSecurityTokenHandler();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ManagedUser?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var keys = await _jwksProvider.GetSecurityKeysAsync(cancellationToken);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = !string.IsNullOrEmpty(_options.Issuer) || !string.IsNullOrEmpty(_options.Authority),
                ValidIssuer = _options.Issuer ?? _options.Authority,
                ValidateAudience = !string.IsNullOrEmpty(_options.Audience),
                ValidAudience = _options.Audience,
                ValidateLifetime = true,
                IssuerSigningKeys = keys,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);

            return new ManagedUser(principal, token);
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
