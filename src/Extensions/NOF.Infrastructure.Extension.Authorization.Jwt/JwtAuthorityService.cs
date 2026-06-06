using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using NOF.Contract;
using NOF.Contract.Extension.Authorization.Jwt;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed partial class JwtAuthorityService : RpcServer<IJwtAuthorityService>;

public sealed class GenerateJwtTokenHandler : JwtAuthorityService.GenerateJwtToken
{
    private readonly ISigningKeyService _signingKeyService;
    private readonly JwtAuthorityOptions _options;

    public GenerateJwtTokenHandler(
        ISigningKeyService signingKeyService,
        IOptions<JwtAuthorityOptions> options)
    {
        _signingKeyService = signingKeyService;
        _options = options.Value;
    }

    public override async Task<Result<GenerateJwtTokenResponse>> HandleAsync(GenerateJwtTokenRequest request, CancellationToken cancellationToken)
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

        IssuedJwtRefreshToken? refreshToken = null;
        if (request.RefreshToken is not null)
        {
            var refreshClaims = CreateClaims(request.RefreshToken.Claims);
            refreshClaims.RemoveAll(claim => claim.Type == ClaimTypes.JwtId);
            refreshClaims.Insert(0, new Claim(ClaimTypes.JwtId, Guid.NewGuid().ToString("N")));

            var refreshTokenExpiresAtUtc = now.Add(request.RefreshToken.Expiration);
            var refreshTokenValue = tokenHandler.WriteToken(new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: request.Audience,
                claims: refreshClaims,
                notBefore: now,
                expires: refreshTokenExpiresAtUtc,
                signingCredentials: signingCredentials));
            refreshToken = new IssuedJwtRefreshToken
            {
                Token = refreshTokenValue,
                ExpiresAtUtc = refreshTokenExpiresAtUtc
            };
        }

        return Result.Success(new GenerateJwtTokenResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
            RefreshToken = refreshToken
        });
    }

    private static List<Claim> CreateClaims(IEnumerable<JwtClaim>? claims)
    {
        return claims?
            .Where(static claim => !string.IsNullOrWhiteSpace(claim.Type))
            .Select(static claim => CreateClaim(claim))
            .ToList()
            ?? [];
    }

    private static Claim CreateClaim(JwtClaim claim)
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

public sealed class ValidateJwtRefreshTokenHandler : JwtAuthorityService.ValidateJwtRefreshToken
{
    private readonly ISigningKeyService _signingKeyService;
    private readonly IRevokedRefreshTokenRepository _revokedRefreshTokenRepository;
    private readonly JwtAuthorityOptions _options;

    public ValidateJwtRefreshTokenHandler(
        ISigningKeyService signingKeyService,
        IRevokedRefreshTokenRepository revokedRefreshTokenRepository,
        IOptions<JwtAuthorityOptions> options)
    {
        _signingKeyService = signingKeyService;
        _revokedRefreshTokenRepository = revokedRefreshTokenRepository;
        _options = options.Value;
    }

    public override async Task<Result<ValidateJwtRefreshTokenResponse>> HandleAsync(ValidateJwtRefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = (await _signingKeyService.GetAllKeysAsync(cancellationToken).ConfigureAwait(false)).Select(k => k.Key),
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(request.RefreshToken, validationParameters, out _);
            var tokenId = principal.FindFirst(ClaimTypes.JwtId)?.Value;

            if (string.IsNullOrWhiteSpace(tokenId))
            {
                return Result.Fail("400", "Invalid refresh token claims.");
            }

            if (await _revokedRefreshTokenRepository.IsRevokedAsync(tokenId, cancellationToken).ConfigureAwait(false))
            {
                return Result.Fail("401", "Refresh token has been revoked.");
            }

            return Result.Success(new ValidateJwtRefreshTokenResponse
            {
                TokenId = tokenId,
                Claims = principal.Claims
                    .Select(static claim => new JwtClaim(claim.Type, claim.Value, claim.ValueType))
                    .ToArray()
            });
        }
        catch (Exception ex)
        {
            return Result.Fail("401", ex.Message);
        }
    }
}

public sealed class RevokeJwtRefreshTokenHandler : JwtAuthorityService.RevokeJwtRefreshToken
{
    private readonly IRevokedRefreshTokenRepository _revokedRefreshTokenRepository;

    public RevokeJwtRefreshTokenHandler(IRevokedRefreshTokenRepository revokedRefreshTokenRepository)
    {
        _revokedRefreshTokenRepository = revokedRefreshTokenRepository;
    }

    public override async Task<Result> HandleAsync(RevokeJwtRefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await _revokedRefreshTokenRepository
            .RevokeAsync(request.TokenId, request.Expiration, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }
}
