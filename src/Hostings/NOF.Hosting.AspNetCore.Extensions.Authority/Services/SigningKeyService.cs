using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace NOF.Hosting.AspNetCore.Extensions.Authority;

/// <summary>
/// Represents a managed signing key with its metadata.
/// </summary>
public sealed class ManagedSigningKey
{
    /// <summary>
    /// The unique key identifier (kid).
    /// </summary>
    public required string Kid { get; init; }

    /// <summary>
    /// The RSA security key used for signing and validation.
    /// </summary>
    public required RsaSecurityKey Key { get; init; }

    /// <summary>
    /// The UTC time when this key was created.
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }
}

/// <summary>
/// Service for managing signing keys with rotation support.
/// </summary>
public interface ISigningKeyService
{
    /// <summary>
    /// Gets the current active signing key used for signing new tokens.
    /// </summary>
    ManagedSigningKey CurrentSigningKey { get; }

    /// <summary>
    /// Gets all active keys (current + retired) that can be used for token validation.
    /// </summary>
    IReadOnlyList<ManagedSigningKey> AllKeys { get; }

    /// <summary>
    /// Rotates the signing key: generates a new key and retires the current one.
    /// Retired keys are kept for validation up to the configured retention count.
    /// </summary>
    void RotateKey();
}

/// <summary>
/// Implementation of signing key management with key rotation support.
/// Keys are randomly generated RSA keys (no deterministic seed).
/// </summary>
public class SigningKeyService : ISigningKeyService
{
    private readonly JwtOptions _options;
    private readonly Lock _lock = new();
    private ManagedSigningKey _currentKey;
    private readonly List<ManagedSigningKey> _allKeys;

    public SigningKeyService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _currentKey = GenerateNewKey();
        _allKeys = [_currentKey];
    }

    /// <inheritdoc />
    public ManagedSigningKey CurrentSigningKey
    {
        get
        {
            lock (_lock)
            {
                return _currentKey;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ManagedSigningKey> AllKeys
    {
        get
        {
            lock (_lock)
            {
                return _allKeys.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public void RotateKey()
    {
        lock (_lock)
        {
            var newKey = GenerateNewKey();

            _allKeys.Insert(0, newKey);
            _currentKey = newKey;

            // Trim retired keys beyond retention count (+1 for the current key)
            var maxKeys = _options.RetiredKeyRetentionCount + 1;
            while (_allKeys.Count > maxKeys)
            {
                _allKeys.RemoveAt(_allKeys.Count - 1);
            }
        }
    }

    private ManagedSigningKey GenerateNewKey()
    {
        var rsa = RSA.Create(_options.KeySize);
        var kid = ComputeKeyId(rsa);
        var key = new RsaSecurityKey(rsa) { KeyId = kid };

        return new ManagedSigningKey
        {
            Kid = kid,
            Key = key,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static string ComputeKeyId(RSA rsa)
    {
        var publicKeyDer = rsa.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(publicKeyDer);
        return Base64UrlEncoder.Encode(hash)[..16];
    }
}
