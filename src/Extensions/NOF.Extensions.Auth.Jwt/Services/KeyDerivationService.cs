using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace NOF;

/// <summary>
/// Service for deriving client-specific keys from a master key.
/// </summary>
public interface IKeyDerivationService
{
    /// <summary>
    /// Derives a client-specific key from the master key.
    /// </summary>
    /// <param name="audience">The audience identifier.</param>
    /// <returns>The derived client key.</returns>
    string DeriveClientKey(string audience);

    /// <summary>
    /// Derives a refresh token key from the master key.
    /// </summary>
    /// <param name="audience">The audience identifier.</param>
    /// <returns>The derived refresh token key.</returns>
    string DeriveRefreshTokenKey(string audience);

    /// <summary>
    /// Creates an RSA security key from a key string.
    /// </summary>
    /// <param name="keyString">The key string in Base64 format.</param>
    /// <returns>The RSA security key.</returns>
    RsaSecurityKey CreateRsaSecurityKey(string keyString);
}

/// <summary>
/// Service for deriving client-specific keys from a master key using in-memory caching.
/// </summary>
public class KeyDerivationService : IKeyDerivationService
{
    private readonly JwtOptions _options;
    private readonly ConcurrentDictionary<string, string> _keyCache;

    public KeyDerivationService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _keyCache = new ConcurrentDictionary<string, string>();
    }

    /// <summary>
    /// Derives a client-specific key from the master key.
    /// </summary>
    /// <param name="audience">The audience identifier.</param>
    /// <returns>The derived client key.</returns>
    public string DeriveClientKey(string audience)
    {
        var cacheKey = $"client_key:{audience}";

        if (_keyCache.TryGetValue(cacheKey, out var cachedKey))
        {
            return cachedKey;
        }

        using var hmac = new HMACSHA256(Convert.FromBase64String(_options.MasterSecurityKey));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(audience));
        var derivedKey = Convert.ToBase64String(hash);

        _keyCache.TryAdd(cacheKey, derivedKey);
        return derivedKey;
    }

    /// <summary>
    /// Derives a refresh token key from the master key.
    /// </summary>
    /// <param name="audience">The audience identifier.</param>
    /// <returns>The derived refresh token key.</returns>
    public string DeriveRefreshTokenKey(string audience)
    {
        var cacheKey = $"refresh_key:{audience}";

        if (_keyCache.TryGetValue(cacheKey, out var cachedKey))
        {
            return cachedKey;
        }

        using var hmac = new HMACSHA256(Convert.FromBase64String(_options.MasterSecurityKey));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(audience + ":refresh"));
        var derivedKey = Convert.ToBase64String(hash);

        _keyCache.TryAdd(cacheKey, derivedKey);
        return derivedKey;
    }

    /// <summary>
    /// Creates an RSA security key from a key string.
    /// </summary>
    /// <param name="keyString">The key string in Base64 format.</param>
    /// <returns>The RSA security key.</returns>
    public RsaSecurityKey CreateRsaSecurityKey(string keyString)
    {
        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(Convert.FromBase64String(keyString), out _);
        return new RsaSecurityKey(rsa);
    }
}
