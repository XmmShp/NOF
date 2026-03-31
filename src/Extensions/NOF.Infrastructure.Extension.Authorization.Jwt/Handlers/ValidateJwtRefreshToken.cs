using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Annotation;
using NOF.Contract;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

[AutoInject(Lifetime.Scoped, RegisterTypes = new[] { typeof(JwtAuthorityService.ValidateJwtRefreshToken) })]
public sealed class ValidateJwtRefreshToken : JwtAuthorityService.ValidateJwtRefreshToken
{
    private readonly ISigningKeyService _signingKeyService;
    private readonly IRevokedRefreshTokenRepository _revokedRefreshTokenRepository;
    private readonly JwtAuthorityOptions _options;

    public ValidateJwtRefreshToken(
        ISigningKeyService signingKeyService,
        IRevokedRefreshTokenRepository revokedRefreshTokenRepository,
        IOptions<JwtAuthorityOptions> options)
    {
        _signingKeyService = signingKeyService;
        _revokedRefreshTokenRepository = revokedRefreshTokenRepository;
        _options = options.Value;
    }

    public async Task<Result<ValidateJwtRefreshTokenResponse>> ValidateJwtRefreshTokenAsync(ValidateJwtRefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = _signingKeyService.AllKeys.Select(k => k.Key),
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(request.RefreshToken, validationParameters, out _);
            var tokenId = principal.FindFirst(ClaimTypes.JwtId)?.Value;
            var userId = principal.FindFirst(ClaimTypes.Subject)?.Value;
            var tenantId = principal.FindFirst(ClaimTypes.TenantId)?.Value;

            if (string.IsNullOrWhiteSpace(tokenId) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(tenantId))
            {
                return Result.Fail("400", "Invalid refresh token claims.");
            }

            if (await _revokedRefreshTokenRepository.IsRevokedAsync(tokenId, cancellationToken).ConfigureAwait(false))
            {
                return Result.Fail("401", "Refresh token has been revoked.");
            }

            return Result.Success(new ValidateJwtRefreshTokenResponse(tokenId, userId, tenantId));
        }
        catch (Exception ex)
        {
            return Result.Fail("401", ex.Message);
        }
    }
}
