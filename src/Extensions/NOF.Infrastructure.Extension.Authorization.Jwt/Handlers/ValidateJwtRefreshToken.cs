using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using NOF.Contract;
using System.IdentityModel.Tokens.Jwt;
using ClaimTypes = System.Security.Claims.ClaimTypes;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Handler for validating refresh token requests.
/// This handler has a single responsibility: validate refresh tokens and check revocation status.
/// It performs read-only operations and never modifies the revocation state.
/// Refresh tokens are long-lived and require revocation checking for security.
/// </summary>
public class ValidateJwtRefreshToken : IRequestHandler<ValidateJwtRefreshTokenRequest, ValidateJwtRefreshTokenResponse>
{
    private readonly JwtAuthorityOptions _options;
    private readonly IRevokedRefreshTokenRepository _repository;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ISigningKeyService _signingKeyService;

    public ValidateJwtRefreshToken(IOptions<JwtAuthorityOptions> options, IRevokedRefreshTokenRepository repository, ISigningKeyService signingKeyService)
    {
        _options = options.Value;
        _repository = repository;
        _tokenHandler = new JwtSecurityTokenHandler();
        _signingKeyService = signingKeyService;
    }

    public async Task<Result<ValidateJwtRefreshTokenResponse>> HandleAsync(ValidateJwtRefreshTokenRequest request, CancellationToken cancellationToken = default)
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
            var tokenId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.JwtId)?.Value;
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Subject)?.Value;
            var tenantId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.TenantId)?.Value;

            if (string.IsNullOrEmpty(tokenId) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
            {
                return Result.Fail(400, "Invalid refresh token claims");
            }

            // Check if the refresh token has been revoked
            var isRevoked = await _repository.IsRevokedAsync(tokenId, cancellationToken);
            if (isRevoked)
            {
                return Result.Fail(400, "Refresh token has been revoked");
            }

            return Result.Success(new ValidateJwtRefreshTokenResponse(tokenId, userId, tenantId));
        }
        catch (Exception ex)
        {
            return Result.Fail(500, $"An unexpected error occurred: {ex.Message}");
        }
    }
}
