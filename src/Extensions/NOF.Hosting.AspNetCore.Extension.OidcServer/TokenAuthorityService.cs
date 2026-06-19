using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Contract;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed partial class TokenAuthorityService : ITokenService
{
    private readonly ISigningKeyService _signingKeyService;
    private readonly IRevokedRefreshTokenRepository _revokedRefreshTokenRepository;
    private readonly OAuthAuthorizationServerOptions _options;

    public TokenAuthorityService(
        ISigningKeyService signingKeyService,
        IRevokedRefreshTokenRepository revokedRefreshTokenRepository,
        IOptions<OAuthAuthorizationServerOptions> options)
    {
        _signingKeyService = signingKeyService;
        _revokedRefreshTokenRepository = revokedRefreshTokenRepository;
        _options = options.Value;
    }

    public async Task<Result<IssueTokenResponse>> IssueTokenAsync(IssueTokenRequest request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var accessClaims = CreateClaims(request.AccessClaims);

        var tokenHandler = new JwtSecurityTokenHandler();
        var signingKey = (await _signingKeyService.GetCurrentSigningKeyAsync(cancellationToken).ConfigureAwait(false)).Key;
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);
        var accessTokenExpiresAtUtc = now.Add(request.AccessTokenExpiration);

        var accessToken = tokenHandler.WriteToken(new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: request.Audience,
            claims: accessClaims,
            notBefore: now,
            expires: accessTokenExpiresAtUtc,
            signingCredentials: signingCredentials));

        IssuedRefreshToken? refreshToken = null;
        if (request.RefreshToken is not null)
        {
            var refreshClaims = CreateClaims(request.RefreshToken.Claims);
            refreshClaims.RemoveAll(claim => claim.Type == JwtRegisteredClaimNames.Jti);
            refreshClaims.Insert(0, new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")));

            var refreshTokenExpiresAtUtc = now.Add(request.RefreshToken.Expiration);
            var refreshTokenValue = tokenHandler.WriteToken(new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: request.Audience,
                claims: refreshClaims,
                notBefore: now,
                expires: refreshTokenExpiresAtUtc,
                signingCredentials: signingCredentials));
            refreshToken = new IssuedRefreshToken
            {
                Token = refreshTokenValue,
                ExpiresAtUtc = refreshTokenExpiresAtUtc
            };
        }

        return Result.Success(new IssueTokenResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
            RefreshToken = refreshToken
        });
    }

    public async Task<Result<ValidateRefreshTokenResponse>> ValidateRefreshTokenAsync(
        ValidateRefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = (await _signingKeyService.GetAllKeysAsync(cancellationToken).ConfigureAwait(false)).Select(static key => key.Key),
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(request.Audience),
                ValidAudience = request.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(request.RefreshToken, validationParameters, out _);
            var tokenId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            if (string.IsNullOrWhiteSpace(tokenId))
            {
                return Result.Fail("400", "Invalid refresh token claims.");
            }

            if (await _revokedRefreshTokenRepository.IsRevokedAsync(tokenId, cancellationToken).ConfigureAwait(false))
            {
                return Result.Fail("401", "Refresh token has been revoked.");
            }

            return Result.Success(new ValidateRefreshTokenResponse
            {
                TokenId = tokenId,
                Claims = principal.Claims
                    .Select(static claim => new TokenClaim(claim.Type, claim.Value, claim.ValueType))
                    .ToArray()
            });
        }
        catch (Exception ex)
        {
            return Result.Fail("401", ex.Message);
        }
    }

    public async Task<Result> RevokeRefreshTokenAsync(RevokeRefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await _revokedRefreshTokenRepository
            .RevokeAsync(request.TokenId, request.Expiration, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    private static List<Claim> CreateClaims(IEnumerable<TokenClaim>? claims)
    {
        return claims?
            .Where(static claim => !string.IsNullOrWhiteSpace(claim.Type))
            .Select(static claim => CreateClaim(claim))
            .ToList()
            ?? [];
    }

    private static Claim CreateClaim(TokenClaim claim)
    {
        var valueType = claim.ValueType;
        if (string.IsNullOrWhiteSpace(valueType) && IsNumericDateClaim(claim.Type) && long.TryParse(claim.Value, out _))
        {
            valueType = ClaimValueTypes.Integer64;
        }

        return string.IsNullOrWhiteSpace(valueType)
            ? new Claim(claim.Type, claim.Value)
            : new Claim(claim.Type, claim.Value, valueType);
    }

    private static bool IsNumericDateClaim(string type)
    {
        return string.Equals(type, JwtRegisteredClaimNames.Iat, StringComparison.Ordinal)
            || string.Equals(type, JwtRegisteredClaimNames.Nbf, StringComparison.Ordinal)
            || string.Equals(type, JwtRegisteredClaimNames.Exp, StringComparison.Ordinal)
            || string.Equals(type, "auth_time", StringComparison.Ordinal);
    }
}
