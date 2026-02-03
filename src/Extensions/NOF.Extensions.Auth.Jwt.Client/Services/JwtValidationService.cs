using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using NOF;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

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
            // Get the token ID (jti) and audience to check if it's revoked and get proper keys
            var jwtToken = _tokenHandler.ReadJwtToken(token);
            var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == NOFJwtConstants.ClaimTypes.JwtId)?.Value;
            var audience = jwtToken.Claims.FirstOrDefault(c => c.Type == NOFJwtConstants.ClaimTypes.Audience)?.Value;

            if (string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(audience))
                return null;

            // Check if token is revoked in cache
            var revokedKey = $"{NOFJwtConstants.RevokedRefreshTokenCachePrefix}{jti}";
            var isRevoked = await _cache.ExistsAsync(revokedKey, cancellationToken);
            if (isRevoked)
                return null;

            // Get validation parameters with JWKS for specific audience
            var validationParameters = await GetValidationParametersAsync(audience, cancellationToken);
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

    private async Task<TokenValidationParameters?> GetValidationParametersAsync(string audience, CancellationToken cancellationToken)
    {
        var keys = await GetJwksAsync(audience, cancellationToken);
        if (keys == null)
            return null;

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

    private async Task<JsonWebKey[]?> GetJwksAsync(string audience, CancellationToken cancellationToken)
    {
        var cacheKey = $"{NOFJwtConstants.JwksCacheKey}:{audience}";
        var cachedKeys = await _cache.GetAsync<JsonWebKey[]>(cacheKey, cancellationToken);
        if (cachedKeys.HasValue)
            return cachedKeys.Value;

        var request = new GetJwksRequest(audience);
        var result = await _requestSender.SendAsync(request, cancellationToken: cancellationToken);

        if (result.IsSuccess && result.Value?.Keys != null)
        {
            await _cache.SetAsync(cacheKey, result.Value.Keys, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _jwksCacheDuration
            }, cancellationToken);

            return result.Value.Keys;
        }

        return null;
    }

    private IEnumerable<SecurityKey> ParseJwksKeys(JsonWebKey[] keys)
    {
        var securityKeys = new List<SecurityKey>();

        foreach (var key in keys)
        {
            if (key.Kty != "RSA")
                continue;

            var n = Convert.FromBase64String(key.N);
            var e = Convert.FromBase64String(key.E);

            var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = n,
                Exponent = e
            });

            securityKeys.Add(new RsaSecurityKey(rsa));
        }

        return securityKeys;
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
