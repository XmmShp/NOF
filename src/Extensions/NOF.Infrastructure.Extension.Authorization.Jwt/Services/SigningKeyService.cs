using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

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
/// Each signing key is persisted as a single row with encrypted private key material.
/// </summary>
public sealed class SigningKeyService : ISigningKeyService
{
    private readonly JwtAuthorityOptions _options;
    private readonly NOFDbContext _dbContext;
    private byte[]? _encryptionKey;
    private readonly Lock _lock = new();
    private ManagedSigningKey _currentKey;
    private readonly List<ManagedSigningKey> _allKeys;

    public SigningKeyService(IOptions<JwtAuthorityOptions> options, NOFDbContext dbContext)
    {
        _options = options.Value;
        _dbContext = dbContext;

        var keys = LoadOrCreateKeys();
        _allKeys = [.. keys];
        _currentKey = _allKeys[0];
    }

    /// <inheritdoc />
    public ManagedSigningKey CurrentSigningKey
    {
        get
        {
            lock (_lock)
            {
                RefreshKeysFromStore();
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
                RefreshKeysFromStore();
                return _allKeys.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public void RotateKey()
    {
        lock (_lock)
        {
            RefreshKeysFromStore();

            var newKey = GenerateNewKey();
            var persistedKeys = PersistRotatedKeys(newKey);

            _allKeys.Clear();
            _allKeys.AddRange(persistedKeys);
            _currentKey = _allKeys[0];
        }
    }

    private void RefreshKeysFromStore()
    {
        var keys = LoadKeysFromStore();
        if (keys is null)
        {
            return;
        }

        _allKeys.Clear();
        _allKeys.AddRange(keys);
        _currentKey = _allKeys[0];
    }

    private List<ManagedSigningKey> LoadOrCreateKeys()
    {
        var keys = LoadKeysFromStore();
        if (keys is not null)
        {
            return keys;
        }

        var createdKey = GenerateNewKey();
        return PersistInitialKey(createdKey);
    }

    private List<ManagedSigningKey>? LoadKeysFromStore()
    {
        EnsureEncryptionKeyInitialized();

        var persistedKeys = _dbContext.Set<PersistedSigningKey>()
            .AsNoTracking()
            .Where(key => key.Status == PersistedSigningKeyStatus.Active || key.Status == PersistedSigningKeyStatus.Retired)
            .OrderBy(key => key.Status == PersistedSigningKeyStatus.Active ? 0 : 1)
            .ThenByDescending(key => key.CreatedAtUtc)
            .ToList();

        if (persistedKeys.Count is 0)
        {
            return null;
        }

        var keys = persistedKeys.Select(ToManagedSigningKey).ToList();
        if (keys.Count is 0)
        {
            throw new InvalidOperationException("No usable signing keys were loaded from persistence.");
        }

        return keys;
    }

    private List<ManagedSigningKey> PersistInitialKey(ManagedSigningKey key)
    {
        EnsureEncryptionKeyInitialized();

        var existing = _dbContext.Set<PersistedSigningKey>()
            .AsNoTracking()
            .Any(persisted => persisted.Status == PersistedSigningKeyStatus.Active || persisted.Status == PersistedSigningKeyStatus.Retired);

        if (!existing)
        {
            var now = DateTime.UtcNow;
            _dbContext.Add(ToPersistedSigningKey(key, PersistedSigningKeyStatus.Active, now));
        }

        try
        {
            _dbContext.SaveChanges();

            var keys = LoadKeysFromStore();
            if (keys is not null)
            {
                return keys;
            }

            throw new InvalidOperationException("Failed to persist initial signing key.");
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            var latestKeys = LoadKeysFromStore();
            if (latestKeys is not null)
            {
                return latestKeys;
            }

            throw;
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            var latestKeys = LoadKeysFromStore();
            if (latestKeys is not null)
            {
                return latestKeys;
            }

            throw;
        }
    }

    private List<ManagedSigningKey> PersistRotatedKeys(ManagedSigningKey newKey)
    {
        EnsureEncryptionKeyInitialized();

        var now = DateTime.UtcNow;
        var activeAndRetired = _dbContext.Set<PersistedSigningKey>()
            .Where(key => key.Status == PersistedSigningKeyStatus.Active || key.Status == PersistedSigningKeyStatus.Retired)
            .OrderByDescending(key => key.CreatedAtUtc)
            .ToList();

        foreach (var activeKey in activeAndRetired.Where(key => key.Status == PersistedSigningKeyStatus.Active))
        {
            activeKey.Status = PersistedSigningKeyStatus.Retired;
            activeKey.UpdatedAtUtc = now;
            activeKey.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        }

        var retiredKeys = activeAndRetired
            .Where(key => key.Status == PersistedSigningKeyStatus.Retired)
            .OrderByDescending(key => key.CreatedAtUtc)
            .ToList();

        for (var index = _options.RetiredKeyRetentionCount; index < retiredKeys.Count; index++)
        {
            var retiredKey = retiredKeys[index];
            retiredKey.Status = PersistedSigningKeyStatus.Revoked;
            retiredKey.InvalidatedAtUtc = now;
            retiredKey.UpdatedAtUtc = now;
            retiredKey.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        }

        _dbContext.Add(ToPersistedSigningKey(newKey, PersistedSigningKeyStatus.Active, now));

        try
        {
            _dbContext.SaveChanges();

            var keys = LoadKeysFromStore();
            if (keys is not null)
            {
                return keys;
            }

            throw new InvalidOperationException("Failed to persist rotated signing keys.");
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            var latestKeys = LoadKeysFromStore();
            if (latestKeys is not null)
            {
                return latestKeys;
            }

            throw;
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            var latestKeys = LoadKeysFromStore();
            if (latestKeys is not null)
            {
                return latestKeys;
            }

            throw;
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

    private PersistedSigningKey ToPersistedSigningKey(
        ManagedSigningKey key,
        PersistedSigningKeyStatus status,
        DateTime now)
    {
        return new PersistedSigningKey
        {
            Kid = key.Kid,
            EncryptedPrivateKey = Encrypt(Convert.ToBase64String(key.Key.Rsa.ExportPkcs8PrivateKey())),
            PublicKey = Convert.ToBase64String(key.Key.Rsa.ExportSubjectPublicKeyInfo()),
            Status = status,
            CreatedAtUtc = key.CreatedAtUtc,
            UpdatedAtUtc = now,
            InvalidatedAtUtc = status == PersistedSigningKeyStatus.Revoked ? now : null,
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
    }

    private ManagedSigningKey ToManagedSigningKey(PersistedSigningKey persistedKey)
    {
        var privateKeyBase64 = Decrypt(persistedKey.EncryptedPrivateKey);
        var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);
        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);

        var computedKid = ComputeKeyId(rsa);
        if (!string.Equals(computedKid, persistedKey.Kid, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Persisted signing key '{persistedKey.Kid}' failed integrity validation.");
        }

        return new ManagedSigningKey
        {
            Kid = persistedKey.Kid,
            Key = new RsaSecurityKey(rsa) { KeyId = persistedKey.Kid },
            CreatedAtUtc = persistedKey.CreatedAtUtc
        };
    }

    private static byte[] GetEncryptionKey(string signingKeyEncryptionKey)
    {
        if (string.IsNullOrWhiteSpace(signingKeyEncryptionKey))
        {
            throw new InvalidOperationException("SigningKeyEncryptionKey must be a base64 string that decodes to 16/24/32 bytes.");
        }

        byte[] encryptionKey;
        try
        {
            encryptionKey = Convert.FromBase64String(signingKeyEncryptionKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("SigningKeyEncryptionKey must be a base64-encoded AES key.", ex);
        }

        if (encryptionKey.Length is not 16 and not 24 and not 32)
        {
            throw new InvalidOperationException("SigningKeyEncryptionKey must decode to 16, 24, or 32 bytes.");
        }

        return encryptionKey;
    }

    private void EnsureEncryptionKeyInitialized()
    {
        if (_encryptionKey is not null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_options.SigningKeyEncryptionKey))
        {
            _encryptionKey = GetEncryptionKey(_options.SigningKeyEncryptionKey);
            return;
        }

        _encryptionKey = RandomNumberGenerator.GetBytes(32);
    }

    private string Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherText = new byte[plaintextBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_encryptionKey!, 16);
        aes.Encrypt(nonce, plaintextBytes, cipherText, tag);

        var protectedBytes = new byte[nonce.Length + tag.Length + cipherText.Length];
        Buffer.BlockCopy(nonce, 0, protectedBytes, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, protectedBytes, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherText, 0, protectedBytes, nonce.Length + tag.Length, cipherText.Length);

        return Convert.ToBase64String(protectedBytes);
    }

    private string Decrypt(string protectedText)
    {
        var protectedBytes = Convert.FromBase64String(protectedText);
        if (protectedBytes.Length < 28)
        {
            throw new InvalidOperationException("Persisted signing key payload is invalid.");
        }

        var nonce = protectedBytes[..12];
        var tag = protectedBytes[12..28];
        var cipherText = protectedBytes[28..];
        var plaintextBytes = new byte[cipherText.Length];

        using var aes = new AesGcm(_encryptionKey!, 16);
        aes.Decrypt(nonce, cipherText, tag, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private static string ComputeKeyId(RSA rsa)
    {
        var publicKeyDer = rsa.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(publicKeyDer);
        return Base64UrlEncoder.Encode(hash)[..16];
    }
}
