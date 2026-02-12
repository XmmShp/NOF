using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure.Core;
using System.IdentityModel.Tokens.Jwt;

namespace NOF.Hosting.AspNetCore.Extensions.Authority;

/// <summary>
/// Handler for validating refresh token requests.
/// This handler has a single responsibility: validate refresh tokens and check revocation status.
/// It performs read-only operations and never modifies the cache state.
/// Refresh tokens are long-lived and require revocation checking for security.
/// </summary>
public class JwtValidateRefreshToken : IRequestHandler<JwtValidateRefreshTokenRequest, JwtValidateRefreshTokenResponse>
{
    private readonly AuthorityOptions _options;
    private readonly ICacheService _cache;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ISigningKeyService _signingKeyService;

    public JwtValidateRefreshToken(IOptions<AuthorityOptions> options, ICacheService cache, ISigningKeyService signingKeyService)
    {
        _options = options.Value;
        _cache = cache;
        _tokenHandler = new JwtSecurityTokenHandler();
        _signingKeyService = signingKeyService;
    }

    public async Task<Result<JwtValidateRefreshTokenResponse>> HandleAsync(JwtValidateRefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate the JWT refresh token
            JwtSecurityToken? jwtToken;
            try
            {
                // Use all active keys (current + retired) for validation to support key rotation
                var allKeys = _signingKeyService.AllKeys.Select(SecurityKey (k) => k.Key).ToList();

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _options.Issuer,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    IssuerSigningKeys = allKeys,
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
            var tokenId = jwtToken.Claims.FirstOrDefault(c => c.Type == NOFInfrastructureCoreConstants.Jwt.ClaimTypes.JwtId)?.Value;
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == NOFInfrastructureCoreConstants.Jwt.ClaimTypes.Subject)?.Value;
            var tenantId = jwtToken.Claims.FirstOrDefault(c => c.Type == NOFInfrastructureCoreConstants.Jwt.ClaimTypes.TenantId)?.Value;

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

            return Result.Success(new JwtValidateRefreshTokenResponse(tokenId, userId, tenantId));
        }
        catch (Exception ex)
        {
            return Result.Fail(500, $"An unexpected error occurred: {ex.Message}");
        }
    }
}
