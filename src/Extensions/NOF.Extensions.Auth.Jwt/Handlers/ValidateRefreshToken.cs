using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace NOF;

/// <summary>
/// Handler for validating refresh token requests.
/// This handler has a single responsibility: validate refresh tokens and check revocation status.
/// It performs read-only operations and never modifies the cache state.
/// Refresh tokens are long-lived and require revocation checking for security.
/// </summary>
public class ValidateRefreshToken : IRequestHandler<ValidateRefreshTokenRequest, ValidateRefreshTokenResponse>
{
    private readonly JwtOptions _options;
    private readonly ICacheService _cache;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly IKeyDerivationService _keyDerivationService;

    public ValidateRefreshToken(IOptions<JwtOptions> options, ICacheService cache, IKeyDerivationService keyDerivationService)
    {
        _options = options.Value;
        _cache = cache;
        _tokenHandler = new JwtSecurityTokenHandler();
        _keyDerivationService = keyDerivationService;
    }

    public async Task<Result<ValidateRefreshTokenResponse>> HandleAsync(ValidateRefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate the JWT refresh token
            JwtSecurityToken? jwtToken;
            try
            {
                // First read the token to extract audience for key derivation
                var tokenForAudience = _tokenHandler.ReadJwtToken(request.RefreshToken);
                var audience = tokenForAudience.Claims.FirstOrDefault(c => c.Type == NOFJwtConstants.ClaimTypes.Audience)?.Value;

                if (string.IsNullOrEmpty(audience))
                {
                    return Result.Fail(400, "Invalid refresh token: missing audience");
                }

                // Derive refresh token key from master key
                var refreshTokenKey = _keyDerivationService.DeriveRefreshTokenKey(audience);
                var refreshSigningKey = _keyDerivationService.CreateRsaSecurityKey(refreshTokenKey);

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    IssuerSigningKey = refreshSigningKey,
                    ClockSkew = TimeSpan.Zero
                };

                _tokenHandler.ValidateToken(request.RefreshToken, tokenValidationParameters, out var validatedToken);
                jwtToken = validatedToken as JwtSecurityToken;

                if (jwtToken is null)
                {
                    return Result.Fail(400, "Invalid refresh token format");
                }
            }
            catch (SecurityTokenExpiredException)
            {
                return Result.Fail(400, "Refresh token expired");
            }
            catch (SecurityTokenValidationException ex)
            {
                return Result.Fail(400, $"Invalid refresh token: {ex.Message}");
            }
            catch (Exception ex)
            {
                return Result.Fail(500, $"Error validating refresh token: {ex.Message}");
            }

            // Extract essential claims from the validated token
            var tokenId = jwtToken.Claims.FirstOrDefault(c => c.Type == NOFJwtConstants.ClaimTypes.JwtId)?.Value;
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == NOFJwtConstants.ClaimTypes.Subject)?.Value;
            var tenantId = jwtToken.Claims.FirstOrDefault(c => c.Type == NOFJwtConstants.ClaimTypes.TenantId)?.Value;

            if (string.IsNullOrEmpty(tokenId) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
            {
                return Result.Fail(400, "Invalid refresh token claims");
            }

            // Check if the refresh token has been revoked (always check cache for security)
            var revokedTokenKey = new RevokedRefreshTokenCacheKey(tokenId);
            var isRevoked = await _cache.GetAsync(revokedTokenKey, cancellationToken);
            if (isRevoked.HasValue)
            {
                return Result.Fail(400, "Refresh token has been revoked");
            }

            return Result.Success(new ValidateRefreshTokenResponse(tokenId, userId, tenantId));
        }
        catch (Exception ex)
        {
            return Result.Fail(500, $"An unexpected error occurred: {ex.Message}");
        }
    }
}
