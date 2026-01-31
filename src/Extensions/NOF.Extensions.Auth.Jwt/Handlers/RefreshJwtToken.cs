using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;

namespace NOF;

/// <summary>
/// Handler for refreshing JWT token requests.
/// </summary>
public class RefreshJwtToken : IRequestHandler<RefreshJwtTokenRequest, RefreshJwtTokenResponse>
{
    private readonly JwtOptions _options;
    private readonly ICacheService _cache;
    private readonly RsaSecurityKey _signingKey;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public RefreshJwtToken(IOptions<JwtOptions> options, ICacheService cache)
    {
        _options = options.Value;
        _cache = cache;
        _tokenHandler = new JwtSecurityTokenHandler();
        _signingKey = CreateRsaSecurityKey(_options.SecurityKey);
    }

    public async Task<Result<RefreshJwtTokenResponse>> HandleAsync(RefreshJwtTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate refresh token and extract data
            var refreshTokenData = JsonSerializer.Deserialize<Dictionary<string, object>>(
                Convert.FromBase64String(request.RefreshToken));

            if (refreshTokenData == null || !refreshTokenData.TryGetValue("TokenId", out var value))
                return Result.Fail(400, "Invalid refresh token");

            var tokenId = value.ToString();
            if (string.IsNullOrEmpty(tokenId))
                return Result.Fail(400, "Invalid refresh token");

            // Get cached refresh token data
            var cachedData = await GetRefreshTokenAsync(tokenId, cancellationToken);
            if (cachedData == null)
                return Result.Fail(400, "Refresh token not found or expired");

            // Revoke the old refresh token
            await RemoveRefreshTokenAsync(tokenId, cancellationToken);

            // Generate new token pair
            var now = DateTime.UtcNow;
            var accessTokenExpires = now.AddMinutes(_options.AccessTokenExpirationMinutes);
            var refreshTokenExpires = now.AddDays(_options.RefreshTokenExpirationDays);

            // Generate unique token IDs
            var jti = Guid.NewGuid().ToString();
            var newRefreshTokenId = Guid.NewGuid().ToString();

            // Create claims
            var claims = new JwtClaims
            {
                Jti = jti,
                Sub = cachedData.UserId,
                TenantId = cachedData.UserId,
                Roles = cachedData.Roles ?? [],
                Permissions = cachedData.Permissions ?? [],
                CustomClaims = cachedData.CustomClaims ?? new Dictionary<string, string>(),
                Iat = now,
                Exp = accessTokenExpires,
                Iss = _options.Issuer,
                Aud = _options.Audience
            };

            // Create access token
            var accessToken = CreateAccessToken(claims);

            // Create refresh token
            var refreshToken = CreateRefreshToken(newRefreshTokenId, cachedData.UserId, refreshTokenExpires);

            // Store refresh token in cache for validation
            var newRefreshTokenData = new RefreshTokenData
            {
                TokenId = newRefreshTokenId,
                UserId = cachedData.UserId,
                Expires = refreshTokenExpires,
                Roles = cachedData.Roles,
                Permissions = cachedData.Permissions,
                CustomClaims = cachedData.CustomClaims
            };

            await StoreRefreshTokenAsync(newRefreshTokenId, newRefreshTokenData, refreshTokenExpires, cancellationToken);

            var tokenPair = new TokenPair
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = accessTokenExpires,
                RefreshTokenExpiresAt = refreshTokenExpires,
                TokenType = NOFJwtConstants.TokenType
            };

            return Result.Success(new RefreshJwtTokenResponse(tokenPair));
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

    private async Task<RefreshTokenData?> GetRefreshTokenAsync(string tokenId, CancellationToken cancellationToken)
    {
        var cacheKey = new RefreshTokenCacheKey(tokenId);
        var result = await _cache.GetAsync<RefreshTokenData>(cacheKey, cancellationToken);
        return result.HasValue ? result.Value : null;
    }

    private async Task RemoveRefreshTokenAsync(string tokenId, CancellationToken cancellationToken)
    {
        var cacheKey = new RefreshTokenCacheKey(tokenId);
        await _cache.RemoveAsync(cacheKey, cancellationToken);
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
