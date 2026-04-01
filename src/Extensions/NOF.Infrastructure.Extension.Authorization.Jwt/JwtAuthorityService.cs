using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Contract;
using NOF.Contract.Extension.Authorization.Jwt;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed class JwtAuthorityService : IJwtAuthorityService, IJwksService
{
    private readonly ISigningKeyService _signingKeyService;
    private readonly IRevokedRefreshTokenRepository _revokedRefreshTokenRepository;
    private readonly IJwksService _jwksService;
    private readonly JwtAuthorityOptions _options;

    public JwtAuthorityService(
        ISigningKeyService signingKeyService,
        IRevokedRefreshTokenRepository revokedRefreshTokenRepository,
        IJwksService jwksService,
        IOptions<JwtAuthorityOptions> options)
    {
        _signingKeyService = signingKeyService;
        _revokedRefreshTokenRepository = revokedRefreshTokenRepository;
        _jwksService = jwksService;
        _options = options.Value;
    }

    public Task<Result<GenerateJwtTokenResponse>> GenerateJwtTokenAsync(GenerateJwtTokenRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var refreshTokenId = Guid.NewGuid().ToString("N");

        var accessClaims = new List<Claim>
        {
            new(ClaimTypes.Subject, request.UserId),
            new(ClaimTypes.TenantId, request.TenantId)
        };

        if (request.Permissions is { Length: > 0 })
        {
            accessClaims.AddRange(request.Permissions.Select(permission => new Claim(ClaimTypes.Permission, permission)));
        }

        if (request.CustomClaims is not null)
        {
            accessClaims.AddRange(request.CustomClaims.Select(pair => new Claim(pair.Key, pair.Value)));
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var signingKey = _signingKeyService.CurrentSigningKey.Key;
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var accessToken = tokenHandler.WriteToken(new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: request.Audience,
            claims: accessClaims,
            notBefore: now,
            expires: now.Add(request.AccessTokenExpiration),
            signingCredentials: signingCredentials));

        var refreshClaims = new[]
        {
            new Claim(ClaimTypes.JwtId, refreshTokenId),
            new Claim(ClaimTypes.Subject, request.UserId),
            new Claim(ClaimTypes.TenantId, request.TenantId)
        };

        var refreshToken = tokenHandler.WriteToken(new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: request.Audience,
            claims: refreshClaims,
            notBefore: now,
            expires: now.Add(request.RefreshTokenExpiration),
            signingCredentials: signingCredentials));

        var tokenPair = new TokenPair
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = now.Add(request.AccessTokenExpiration),
            RefreshTokenExpiresAt = now.Add(request.RefreshTokenExpiration)
        };

        return Task.FromResult(Result.Success(new GenerateJwtTokenResponse(tokenPair)));
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

    public async Task<Result> RevokeJwtRefreshTokenAsync(RevokeJwtRefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        await _revokedRefreshTokenRepository
            .RevokeAsync(request.TokenId, request.Expiration, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public Task<NOF.Contract.Extension.Authorization.Jwt.JwksDocument> GetJwksAsync(CancellationToken cancellationToken = default)
    {
        return _jwksService.GetJwksAsync(cancellationToken);
    }
}
