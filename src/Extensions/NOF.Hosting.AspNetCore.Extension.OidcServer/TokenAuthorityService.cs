using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Contract;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed partial class TokenAuthorityService : ITokenService
{
    private const string AccessTokenTypeHeader = "at+jwt";
    private const string JwtTypeHeader = "JWT";

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
        if (!TryCreateAccessClaims(request, out var accessClaims, out var accessClaimError))
        {
            return Result.Fail("400", accessClaimError);
        }

        var now = DateTime.UtcNow;
        var tokenHandler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };
        var signingKey = (await _signingKeyService.GetCurrentSigningKeyAsync(cancellationToken).ConfigureAwait(false)).Key;
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);
        var accessTokenExpiresAtUtc = now.Add(request.AccessTokenExpiration);
        var accessTokenJwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: request.Audience,
            claims: accessClaims,
            notBefore: now,
            expires: accessTokenExpiresAtUtc,
            signingCredentials: signingCredentials);
        accessTokenJwt.Header["typ"] = AccessTokenTypeHeader;
        var accessToken = tokenHandler.WriteToken(accessTokenJwt);

        IssuedRefreshToken? refreshToken = null;
        if (request.RefreshToken is not null)
        {
            var refreshClaims = CreateRefreshClaims(request);
            refreshClaims.RemoveAll(claim => claim.Type == JwtRegisteredClaimNames.Jti);
            refreshClaims.Insert(0, new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")));

            var refreshTokenExpiresAtUtc = now.Add(request.RefreshToken.Expiration);
            var refreshTokenJwt = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: request.Audience,
                claims: refreshClaims,
                notBefore: now,
                expires: refreshTokenExpiresAtUtc,
                signingCredentials: signingCredentials);
            refreshTokenJwt.Header["typ"] = JwtTypeHeader;
            var refreshTokenValue = tokenHandler.WriteToken(refreshTokenJwt);
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
        var tokenHandler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };
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
                Claims = ToTokenClaims(principal.Claims)
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

    public async Task<Result<IntrospectTokenResponse>> IntrospectTokenAsync(
        IntrospectTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result.Fail("400", "token is required.");
        }

        var tokenTypeHint = request.TokenTypeHint?.Trim();
        if (string.Equals(tokenTypeHint, OAuthTokenTypes.AccessToken, StringComparison.Ordinal))
        {
            return await IntrospectAccessTokenAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(tokenTypeHint, OAuthTokenTypes.RefreshToken, StringComparison.Ordinal))
        {
            return await IntrospectRefreshTokenAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var accessTokenResult = await IntrospectAccessTokenAsync(request, cancellationToken).ConfigureAwait(false);
        if (accessTokenResult.IsSuccess && accessTokenResult.Value.Active)
        {
            return accessTokenResult;
        }

        return await IntrospectRefreshTokenAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static List<Claim> CreateClaims(IEnumerable<TokenClaim>? claims)
    {
        return claims?
            .Where(static claim => !string.IsNullOrWhiteSpace(claim.Type))
            .SelectMany(static claim => CreateClaims(claim))
            .ToList()
            ?? [];
    }

    private static List<Claim> CreateRefreshClaims(IssueTokenRequest request)
    {
        var claims = request.RefreshToken?.Claims?
            .Where(static claim => !string.IsNullOrWhiteSpace(claim.Type))
            .Where(static claim => !string.Equals(claim.Type, OAuthClaimTypes.ClientId, StringComparison.Ordinal))
            .Where(static claim => !string.Equals(claim.Type, JwtRegisteredClaimNames.Iat, StringComparison.Ordinal))
            .ToList()
            ?? [];
        claims.Add(new TokenClaim(OAuthClaimTypes.ClientId, request.ClientId));
        claims.Add(TokenClaim.Integer64(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        return CreateClaims(claims);
    }

    private async Task<Result<IntrospectTokenResponse>> IntrospectAccessTokenAsync(
        IntrospectTokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };
            var principal = tokenHandler.ValidateToken(
                request.Token.Trim(),
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = (await _signingKeyService.GetAllKeysAsync(cancellationToken).ConfigureAwait(false)).Select(static key => key.Key),
                    ValidateIssuer = true,
                    ValidIssuer = _options.Issuer,
                    ValidateAudience = !string.IsNullOrWhiteSpace(request.Audience),
                    ValidAudience = request.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                },
                out var validatedToken);

            return Result.Success(new IntrospectTokenResponse
            {
                Active = true,
                TokenType = OAuthTokenTypes.AccessToken,
                Claims = ToTokenClaims(
                    principal.Claims.Concat(
                        GetAudienceClaims(validatedToken)
                            .SelectMany(static claim => CreateClaims(claim))))
            });
        }
        catch
        {
            return Result.Success(new IntrospectTokenResponse
            {
                Active = false,
                TokenType = OAuthTokenTypes.AccessToken
            });
        }
    }

    private async Task<Result<IntrospectTokenResponse>> IntrospectRefreshTokenAsync(
        IntrospectTokenRequest request,
        CancellationToken cancellationToken)
    {
        var validateResult = await ValidateRefreshTokenAsync(
            new ValidateRefreshTokenRequest
            {
                RefreshToken = request.Token,
                Audience = request.Audience
            },
            cancellationToken).ConfigureAwait(false);
        if (!validateResult.IsSuccess)
        {
            return Result.Success(new IntrospectTokenResponse
            {
                Active = false,
                TokenType = OAuthTokenTypes.RefreshToken
            });
        }

        return Result.Success(new IntrospectTokenResponse
        {
            Active = true,
            TokenType = OAuthTokenTypes.RefreshToken,
            Claims = validateResult.Value.Claims
        });
    }

    private static IEnumerable<TokenClaim> GetAudienceClaims(SecurityToken validatedToken)
    {
        if (validatedToken is not JwtSecurityToken jwtToken)
        {
            yield break;
        }

        foreach (var audience in jwtToken.Audiences)
        {
            yield return new TokenClaim(JwtRegisteredClaimNames.Aud, audience);
        }
    }

    private static IEnumerable<Claim> CreateClaims(TokenClaim claim)
    {
        var valueType = NormalizeValueType(claim);
        foreach (var value in ResolveValues(claim))
        {
            yield return string.IsNullOrWhiteSpace(valueType)
                ? new Claim(claim.Type, value)
                : new Claim(claim.Type, value, valueType);
        }
    }

    private static string? NormalizeValueType(TokenClaim claim)
    {
        var valueType = claim.ValueType;
        if (string.IsNullOrWhiteSpace(valueType) && IsNumericDateClaim(claim.Type) && long.TryParse(claim.Value, out _))
        {
            valueType = ClaimValueTypes.Integer64;
        }

        return valueType;
    }

    private static IEnumerable<string> ResolveValues(TokenClaim claim)
    {
        if (claim.Values is { Length: > 0 })
        {
            return claim.Values;
        }

        return string.IsNullOrWhiteSpace(claim.Value)
            ? []
            : [claim.Value!];
    }

    private static TokenClaim[] ToTokenClaims(IEnumerable<Claim> claims)
    {
        return claims
            .GroupBy(static claim => (claim.Type, claim.ValueType))
            .Select(static group =>
            {
                var values = group
                    .Select(static claim => claim.Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                return values.Length == 1
                    ? new TokenClaim(group.Key.Type, values[0], group.Key.ValueType)
                    : new TokenClaim
                    {
                        Type = group.Key.Type,
                        Values = values,
                        ValueType = group.Key.ValueType
                    };
            })
            .ToArray();
    }

    private static bool IsNumericDateClaim(string type)
    {
        return string.Equals(type, JwtRegisteredClaimNames.Iat, StringComparison.Ordinal)
            || string.Equals(type, JwtRegisteredClaimNames.Nbf, StringComparison.Ordinal)
            || string.Equals(type, JwtRegisteredClaimNames.Exp, StringComparison.Ordinal)
            || string.Equals(type, "auth_time", StringComparison.Ordinal);
    }

    private static bool TryCreateAccessClaims(IssueTokenRequest request, out List<Claim> claims, out string error)
    {
        claims = [];
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(request.Audience))
        {
            error = "Access token audience is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            error = "Access token client_id is required.";
            return false;
        }

        var normalizedClaims = request.AccessClaims?
            .Where(static claim => !string.IsNullOrWhiteSpace(claim.Type))
            .Where(static claim => !string.Equals(claim.Type, OAuthClaimTypes.ClientId, StringComparison.Ordinal))
            .Where(static claim => !string.Equals(claim.Type, JwtRegisteredClaimNames.Iat, StringComparison.Ordinal))
            .Where(static claim => !string.Equals(claim.Type, JwtRegisteredClaimNames.Jti, StringComparison.Ordinal))
            .ToList()
            ?? [];

        if (!normalizedClaims.Any(static claim => string.Equals(claim.Type, OAuthClaimTypes.Subject, StringComparison.Ordinal)))
        {
            error = "Access token sub is required.";
            return false;
        }

        normalizedClaims.Insert(0, new TokenClaim(OAuthClaimTypes.ClientId, request.ClientId));
        normalizedClaims.Insert(0, new TokenClaim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")));
        normalizedClaims.Insert(0, TokenClaim.Integer64(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        claims = CreateClaims(normalizedClaims);
        return true;
    }
}
