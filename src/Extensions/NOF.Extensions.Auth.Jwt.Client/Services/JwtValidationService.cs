using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using NOF;
using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;

namespace NOF;

/// <summary>
/// Client-side JWT validation service.
/// </summary>
public class JwtValidationService
{
    private readonly IRequestSender _requestSender;
    private readonly ICacheService _cache;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly TimeSpan _jwksCacheDuration = NOFJwtConstants.Expiration.JwksCacheDuration;

    public JwtValidationService(IRequestSender requestSender, ICacheService cache)
    {
        _requestSender = requestSender;
        _cache = cache;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    /// <summary>
    /// Validates a JWT token locally.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The token claims if valid, null otherwise.</returns>
    public async Task<JwtClaims?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the token ID (jti) to check if it's revoked
            var jwtToken = _tokenHandler.ReadJwtToken(token);
            var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == NOFJwtConstants.ClaimTypes.JwtId)?.Value;

            if (string.IsNullOrEmpty(jti))
                return null;

            // Check if token is revoked in cache
            var revokedKey = $"{NOFJwtConstants.RevokedTokenCachePrefix}{jti}";
            var isRevoked = await _cache.ExistsAsync(revokedKey, cancellationToken);
            if (isRevoked)
                return null;

            // Get validation parameters with JWKS
            var validationParameters = await GetValidationParametersAsync(cancellationToken);
            if (validationParameters == null)
                return null;

            // Validate the token
            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);

            // Extract claims
            return ExtractClaims(principal);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Handles token revocation notification.
    /// </summary>
    /// <param name="notification">The revocation notification.</param>
    public async Task HandleTokenRevokedAsync(TokenRevokedNotification notification, CancellationToken cancellationToken = default)
    {
        // Cache the revoked token ID
        var revokedKey = $"{NOFJwtConstants.RevokedTokenCachePrefix}{notification.TokenId}";
        await _cache.SetAsync(revokedKey, true, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = NOFJwtConstants.Expiration.RevokedTokenCacheDuration
        }, cancellationToken);

        // If user ID is provided, cache user revocation
        if (!string.IsNullOrEmpty(notification.UserId))
        {
            var userRevokedKey = $"{NOFJwtConstants.RevokedUserCachePrefix}{notification.UserId}";
            await _cache.SetAsync(userRevokedKey, true, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = NOFJwtConstants.Expiration.RevokedTokenCacheDuration
            }, cancellationToken);
        }
    }

    private async Task<TokenValidationParameters?> GetValidationParametersAsync(CancellationToken cancellationToken)
    {
        var jwks = await GetJwksAsync(cancellationToken);
        if (string.IsNullOrEmpty(jwks))
            return null;

        var jwksDocument = JsonDocument.Parse(jwks);
        var keys = jwksDocument.RootElement.GetProperty("keys");

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            IssuerSigningKeys = ParseJwksKeys(keys)
        };

        return validationParameters;
    }

    private async Task<string?> GetJwksAsync(CancellationToken cancellationToken)
    {
        var cachedJwks = await _cache.GetAsync<string>(NOFJwtConstants.JwksCacheKey, cancellationToken);
        if (cachedJwks.HasValue)
            return cachedJwks.Value;

        var request = new GetJwksRequest();
        var result = await _requestSender.SendAsync(request, cancellationToken: cancellationToken);

        if (result.IsSuccess && result.Value?.JwksJson != null)
        {
            await _cache.SetAsync(NOFJwtConstants.JwksCacheKey, result.Value.JwksJson, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _jwksCacheDuration
            }, cancellationToken);

            return result.Value.JwksJson;
        }

        return null;
    }

    private IEnumerable<SecurityKey> ParseJwksKeys(JsonElement keysElement)
    {
        var keys = new List<SecurityKey>();

        foreach (var keyElement in keysElement.EnumerateArray())
        {
            if (keyElement.GetProperty("kty").GetString() != "RSA")
                continue;

            var n = Base64Url.DecodeBytes(keyElement.GetProperty("n").GetString()!);
            var e = Base64Url.DecodeBytes(keyElement.GetProperty("e").GetString()!);

            var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = n,
                Exponent = e
            });

            keys.Add(new RsaSecurityKey(rsa));
        }

        return keys;
    }

    private JwtClaims ExtractClaims(System.Security.Claims.ClaimsPrincipal principal)
    {
        var claims = new JwtClaims
        {
            Jti = principal.FindFirst(NOFJwtConstants.ClaimTypes.JwtId)?.Value ?? string.Empty,
            Sub = principal.FindFirst(NOFJwtConstants.ClaimTypes.Subject)?.Value ?? string.Empty,
            TenantId = principal.FindFirst(NOFJwtConstants.ClaimTypes.TenantId)?.Value ?? string.Empty,
            Iss = principal.FindFirst(NOFJwtConstants.ClaimTypes.Issuer)?.Value ?? string.Empty,
            Aud = principal.FindFirst(NOFJwtConstants.ClaimTypes.Audience)?.Value ?? string.Empty,
            Iat = DateTimeOffset.FromUnixTimeSeconds(long.Parse(principal.FindFirst(NOFJwtConstants.ClaimTypes.IssuedAt)?.Value ?? "0")).DateTime,
            Exp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(principal.FindFirst(NOFJwtConstants.ClaimTypes.ExpiresAt)?.Value ?? "0")).DateTime,
            Roles = principal.FindAll(NOFJwtConstants.ClaimTypes.Role).Select(c => c.Value).ToList(),
            Permissions = principal.FindAll(NOFJwtConstants.ClaimTypes.Permission).Select(c => c.Value).ToList()
        };

        // Add custom claims
        foreach (var claim in principal.Claims.Where(c => !c.Type.Equals(NOFJwtConstants.ClaimTypes.JwtId) && !c.Type.Equals(NOFJwtConstants.ClaimTypes.Subject) &&
                                                          !c.Type.Equals(NOFJwtConstants.ClaimTypes.TenantId) && !c.Type.Equals(NOFJwtConstants.ClaimTypes.Issuer) && !c.Type.Equals(NOFJwtConstants.ClaimTypes.Audience) &&
                                                          !c.Type.Equals(NOFJwtConstants.ClaimTypes.IssuedAt) && !c.Type.Equals(NOFJwtConstants.ClaimTypes.ExpiresAt) &&
                                                          !c.Type.Equals(NOFJwtConstants.ClaimTypes.Role) && !c.Type.Equals(NOFJwtConstants.ClaimTypes.Permission)))
        {
            claims.CustomClaims[claim.Type] = claim.Value;
        }

        return claims;
    }
}
