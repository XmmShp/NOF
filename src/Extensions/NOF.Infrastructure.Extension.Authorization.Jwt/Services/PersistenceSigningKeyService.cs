using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Implementation of signing key management with key rotation support.
/// Each signing key is persisted as a single row with encrypted private key material.
/// </summary>
public sealed class PersistenceSigningKeyService : ISigningKeyService
{
    private readonly JwtAuthorityOptions _options;
    private readonly NOFDbContext _dbContext;
    private readonly ILogger<PersistenceSigningKeyService> _logger;
    private byte[]? _encryptionKey;

    public PersistenceSigningKeyService(
        IOptions<JwtAuthorityOptions> options,
        NOFDbContext dbContext,
        ILogger<PersistenceSigningKeyService> logger)
    {
        _options = options.Value;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ManagedSigningKey> GetCurrentSigningKeyAsync(CancellationToken cancellationToken = default)
    {
        var keys = await LoadKeysAsync(cancellationToken).ConfigureAwait(false);
        if (keys is not { Count: > 0 })
        {
            throw new InvalidOperationException("No active signing keys were found. Ensure PersistedSigningKeyInitializationStep has completed successfully.");
        }

        return keys[0];
    }

    /// <inheritdoc />
    public async Task<ManagedSigningKey[]> GetAllKeysAsync(CancellationToken cancellationToken = default)
    {
        return [.. await LoadKeysAsync(cancellationToken).ConfigureAwait(false) ?? []];
    }

    /// <inheritdoc />
    public async Task RotateKeyAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - _options.RevokedSigningKeyRetention;
        await _dbContext.Set<PersistedSigningKey>()
            .Where(key => key.Status == PersistedSigningKeyStatus.Revoked
                && key.InvalidatedAtUtc != null
                && key.InvalidatedAtUtc <= cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var newKey = GenerateNewKey();
        EnsureEncryptionKeyInitialized();

        var now = DateTime.UtcNow;
        var activeAndRetired = await _dbContext.Set<PersistedSigningKey>()
            .Where(key => key.Status == PersistedSigningKeyStatus.Active || key.Status == PersistedSigningKeyStatus.Retired)
            .OrderByDescending(key => key.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (activeAndRetired.Count == 0)
        {
            throw new InvalidOperationException("No active signing keys were found. Ensure PersistedSigningKeyInitializationStep has completed successfully.");
        }

        foreach (var activeKey in activeAndRetired.Where(key => key.Status == PersistedSigningKeyStatus.Active))
        {
            activeKey.Status = PersistedSigningKeyStatus.Retired;
            activeKey.UpdatedAtUtc = now;
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
        }

        _dbContext.Add(ToPersistedSigningKey(newKey, PersistedSigningKeyStatus.Active, now));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<List<ManagedSigningKey>?> LoadKeysAsync(CancellationToken cancellationToken)
    {
        EnsureEncryptionKeyInitialized();

        var persistedKeys = await _dbContext.Set<PersistedSigningKey>()
            .AsNoTracking()
            .Where(key => key.Status == PersistedSigningKeyStatus.Active || key.Status == PersistedSigningKeyStatus.Retired)
            .OrderBy(key => key.Status == PersistedSigningKeyStatus.Active ? 0 : 1)
            .ThenByDescending(key => key.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (persistedKeys.Count == 0)
        {
            return null;
        }

        var keys = persistedKeys.Select(ToManagedSigningKey).ToList();
        if (keys.Count == 0)
        {
            throw new InvalidOperationException("No usable signing keys were loaded from persistence.");
        }

        return keys;
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
            InvalidatedAtUtc = status == PersistedSigningKeyStatus.Revoked ? now : null
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

    private void EnsureEncryptionKeyInitialized()
    {
        if (_encryptionKey is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.SigningKeyEncryptionKey))
        {
            _options.SigningKeyEncryptionKey = string.IsNullOrWhiteSpace(Environment.MachineName)
                ? "unknown-device"
                : Environment.MachineName;
            _logger.LogWarning(
                "SigningKeyEncryptionKey is not configured. Falling back to device name '{DeviceName}'. This value has been written back to JwtAuthorityOptions for the current process.",
                _options.SigningKeyEncryptionKey);
        }

        if (string.IsNullOrWhiteSpace(_options.SigningKeyEncryptionKey))
        {
            throw new InvalidOperationException("SigningKeyEncryptionKey must be a non-empty string.");
        }

        _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(_options.SigningKeyEncryptionKey));
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
