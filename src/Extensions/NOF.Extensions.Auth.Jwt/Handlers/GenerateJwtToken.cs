using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;

namespace NOF;

/// <summary>
/// Handler for generating JWT token pair requests.
/// </summary>
public class GenerateJwtToken : IRequestHandler<GenerateJwtTokenRequest, GenerateJwtTokenResponse>
{
    private readonly JwtOptions _options;
    private readonly ICacheService _cache;
    private readonly RsaSecurityKey _signingKey;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public GenerateJwtToken(IOptions<JwtOptions> options, ICacheService cache)
    {
        _options = options.Value;
        _cache = cache;
        _tokenHandler = new JwtSecurityTokenHandler();
        _signingKey = CreateRsaSecurityKey(_options.SecurityKey);
    }

    public async Task<Result<GenerateJwtTokenResponse>> HandleAsync(GenerateJwtTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var accessTokenExpires = now.AddMinutes(_options.AccessTokenExpirationMinutes);
            var refreshTokenExpires = now.AddDays(_options.RefreshTokenExpirationDays);

            // Generate unique token IDs
            var jti = Guid.NewGuid().ToString();
            var refreshTokenId = Guid.NewGuid().ToString();

            // Create claims
            var claims = new JwtClaims
            {
                Jti = jti,
                Sub = request.UserId,
                TenantId = request.TenantId,
                Roles = request.Roles ?? [],
                Permissions = request.Permissions ?? [],
                CustomClaims = request.CustomClaims ?? new Dictionary<string, string>(),
                Iat = now,
                Exp = accessTokenExpires,
                Iss = _options.Issuer,
                Aud = _options.Audience
            };

            // Create access token
            var accessToken = CreateAccessToken(claims);

            // Create refresh token
            var refreshToken = CreateRefreshToken(refreshTokenId, request.UserId, refreshTokenExpires);

            // Store refresh token in cache for validation
            var refreshTokenData = new RefreshTokenData
            {
                TokenId = refreshTokenId,
                UserId = request.UserId,
                Expires = refreshTokenExpires,
                Roles = request.Roles,
                Permissions = request.Permissions,
                CustomClaims = request.CustomClaims
            };

            await StoreRefreshTokenAsync(refreshTokenId, refreshTokenData, refreshTokenExpires, cancellationToken);

            var tokenPair = new TokenPair
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = accessTokenExpires,
                RefreshTokenExpiresAt = refreshTokenExpires,
                TokenType = NOFJwtConstants.TokenType
            };

            return Result.Success(new GenerateJwtTokenResponse(tokenPair));
        }
        catch (Exception ex)
        {
            return Result.Fail(500, ex.Message);
        }
    }

    private string CreateAccessToken(JwtClaims claims)
    {
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(NOFJwtConstants.ClaimTypes.JwtId, claims.Jti),
                new System.Security.Claims.Claim(NOFJwtConstants.ClaimTypes.Subject, claims.Sub),
                new System.Security.Claims.Claim(NOFJwtConstants.ClaimTypes.TenantId, claims.TenantId),
                new System.Security.Claims.Claim(NOFJwtConstants.ClaimTypes.IssuedAt, new DateTimeOffset(claims.Iat).ToUnixTimeSeconds().ToString()),
                new System.Security.Claims.Claim(NOFJwtConstants.ClaimTypes.ExpiresAt, new DateTimeOffset(claims.Exp).ToUnixTimeSeconds().ToString())
            }.Concat(claims.Roles.Select(role => new System.Security.Claims.Claim(NOFJwtConstants.ClaimTypes.Role, role)))
             .Concat(claims.Permissions.Select(permission => new System.Security.Claims.Claim(NOFJwtConstants.ClaimTypes.Permission, permission)))
             .Concat(claims.CustomClaims.Select(kv => new System.Security.Claims.Claim(kv.Key, kv.Value)))),

            Expires = claims.Exp,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = new SigningCredentials(_signingKey, _options.Algorithm)
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }

    private string CreateRefreshToken(string tokenId, string userId, DateTime expires)
    {
        var refreshTokenData = new
        {
            TokenId = tokenId,
            UserId = userId,
            Expires = expires,
            CreatedAt = DateTime.UtcNow
        };

        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(refreshTokenData));
    }

    private async Task StoreRefreshTokenAsync(string tokenId, RefreshTokenData data, DateTime expires, CancellationToken cancellationToken)
    {
        var cacheKey = new RefreshTokenCacheKey(tokenId);
        await _cache.SetAsync(cacheKey, data, new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = expires
        }, cancellationToken);
    }

    private RsaSecurityKey CreateRsaSecurityKey(string keyString)
    {
        var rsa = RSA.Create();

        if (!string.IsNullOrEmpty(keyString) && keyString.Length > 100)
        {
            try
            {
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(keyString), out _);
            }
            catch
            {
                rsa = RSA.Create(2048);
            }
        }
        else
        {
            rsa = RSA.Create(2048);
        }

        return new RsaSecurityKey(rsa);
    }
}
