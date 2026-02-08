using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace NOF;

/// <summary>
/// Service for deriving client-specific keys from a master key.
/// </summary>
public interface IKeyDerivationService
{
    /// <summary>
    /// Gets or creates an RSA security key for the specified audience.
    /// </summary>
    /// <param name="audience">The audience identifier.</param>
    /// <returns>The cached or newly created RSA security key.</returns>
    RsaSecurityKey GetOrCreateRsaSecurityKey(string audience);

    /// <summary>
    /// Gets or creates an RSA security key for refresh tokens for the specified audience.
    /// </summary>
    /// <param name="audience">The audience identifier.</param>
    /// <returns>The cached or newly created RSA security key.</returns>
    RsaSecurityKey GetOrCreateRefreshTokenRsaSecurityKey(string audience);

    /// <summary>
    /// Computes the key ID (kid) from an RSA security key using public key DER encoding SHA-256 hash.
    /// </summary>
    /// <param name="rsaKey">The RSA security key.</param>
    /// <returns>The key ID.</returns>
    string ComputeKeyId(RsaSecurityKey rsaKey);
}

/// <summary>
/// Service for deriving client-specific keys from a master key using in-memory caching.
/// </summary>
public class KeyDerivationService : IKeyDerivationService
{
    private readonly JwtOptions _options;
    private readonly ConcurrentDictionary<string, RsaSecurityKey> _clientRsaKeyCache;
    private readonly ConcurrentDictionary<string, RsaSecurityKey> _refreshRsaKeyCache;

    public KeyDerivationService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _clientRsaKeyCache = new ConcurrentDictionary<string, RsaSecurityKey>();
        _refreshRsaKeyCache = new ConcurrentDictionary<string, RsaSecurityKey>();
    }

    /// <summary>
    /// Gets or creates an RSA security key for the specified audience.
    /// </summary>
    /// <param name="audience">The audience identifier.</param>
    /// <returns>The cached or newly created RSA security key.</returns>
    public RsaSecurityKey GetOrCreateRsaSecurityKey(string audience)
    {
        return _clientRsaKeyCache.GetOrAdd(audience, aud =>
        {
            var derivedKeyStr = DeriveClientKey(aud);
            var seedBytes = Encoding.UTF8.GetBytes(derivedKeyStr);
            var rsaParams = GenerateDeterministicRsaParameters(seedBytes);
            var rsa = RSA.Create();
            rsa.ImportParameters(rsaParams);
            return new RsaSecurityKey(rsa);
        });
    }

    /// <summary>
    /// Gets or creates an RSA security key for refresh tokens for the specified audience.
    /// </summary>
    /// <param name="audience">The audience identifier.</param>
    /// <returns>The cached or newly created RSA security key.</returns>
    public RsaSecurityKey GetOrCreateRefreshTokenRsaSecurityKey(string audience)
    {
        return _refreshRsaKeyCache.GetOrAdd(audience, aud =>
        {
            var derivedKeyStr = DeriveRefreshTokenKey(aud);
            var seedBytes = Encoding.UTF8.GetBytes(derivedKeyStr);
            var rsaParams = GenerateDeterministicRsaParameters(seedBytes);
            var rsa = RSA.Create();
            rsa.ImportParameters(rsaParams);
            return new RsaSecurityKey(rsa);
        });
    }

    /// <summary>
    /// Computes the key ID (kid) from an RSA security key using public key DER encoding SHA-256 hash.
    /// </summary>
    /// <param name="rsaKey">The RSA security key.</param>
    /// <returns>The key ID.</returns>
    public string ComputeKeyId(RsaSecurityKey rsaKey)
    {
        // Use SHA-256 digest of the public key DER encoding as kid
        var publicKeyDer = rsaKey.Rsa.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(publicKeyDer);
        return Convert.ToBase64String(hash)[..16]; // Take the first 16 characters as kid
    }

    private string DeriveClientKey(string audience)
    {
        using var hmac = new HMACSHA256(Convert.FromBase64String(_options.MasterSecurityKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(audience));
        return Convert.ToBase64String(hash);
    }

    private string DeriveRefreshTokenKey(string audience)
    {
        using var hmac = new HMACSHA256(Convert.FromBase64String(_options.MasterSecurityKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(audience + ":refresh"));
        return Convert.ToBase64String(hash);
    }

    private static RSAParameters GenerateDeterministicRsaParameters(byte[] seed)
    {
        // Extend the seed to sufficient length using SHA-256 (optional, enhances entropy)
        var extendedSeed = SHA256.HashData(seed);

        // Create a deterministic random source
        var randomGenerator = Org.BouncyCastle.Security.SecureRandom.GetInstance("SHA256PRNG");
        randomGenerator.SetSeed(extendedSeed);

        // Configure RSA key generation parameters
        var keyGenParams = new RsaKeyGenerationParameters(
            publicExponent: BigInteger.ValueOf(65537), // Standard public exponent
            random: randomGenerator,
            strength: 2048,      // Key length
            certainty: 128       // Primality test certainty (higher = more secure, slightly slower)
        );

        // Generate key pair
        var keyGen = new RsaKeyPairGenerator();
        keyGen.Init(keyGenParams);
        var keyPair = keyGen.GenerateKeyPair();

        // Extract private and public key parameters
        var priv = (RsaPrivateCrtKeyParameters)keyPair.Private;
        var pub = (RsaKeyParameters)keyPair.Public;

        // Convert to .NET RSAParameters
        return new RSAParameters
        {
            Modulus = priv.Modulus.ToByteArrayUnsigned(),
            Exponent = pub.Exponent.ToByteArrayUnsigned(),
            D = priv.Exponent.ToByteArrayUnsigned(),
            P = priv.P.ToByteArrayUnsigned(),
            Q = priv.Q.ToByteArrayUnsigned(),
            DP = priv.DP.ToByteArrayUnsigned(),
            DQ = priv.DQ.ToByteArrayUnsigned(),
            InverseQ = priv.QInv.ToByteArrayUnsigned()
        };
    }
}
