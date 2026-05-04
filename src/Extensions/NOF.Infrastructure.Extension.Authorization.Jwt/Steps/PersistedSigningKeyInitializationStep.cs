using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed class PersistedSigningKeyInitializationStep : IDataSeedInitializationStep
{
    public async Task ExecuteAsync(IHost app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NOFDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<JwtAuthorityOptions>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PersistedSigningKeyInitializationStep>>();

        var existing = await dbContext.Set<PersistedSigningKey>()
            .AsNoTracking()
            .AnyAsync(key => key.Status == PersistedSigningKeyStatus.Active || key.Status == PersistedSigningKeyStatus.Retired)
            .ConfigureAwait(false);
        if (existing)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.SigningKeyEncryptionKey))
        {
            options.SigningKeyEncryptionKey = string.IsNullOrWhiteSpace(Environment.MachineName)
                ? "unknown-device"
                : Environment.MachineName;
            logger.LogWarning(
                "SigningKeyEncryptionKey is not configured. Falling back to device name '{DeviceName}'. This value has been written back to JwtAuthorityOptions for the current process.",
                options.SigningKeyEncryptionKey);
        }

        if (string.IsNullOrWhiteSpace(options.SigningKeyEncryptionKey))
        {
            throw new InvalidOperationException("SigningKeyEncryptionKey must be a non-empty string.");
        }

        var encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(options.SigningKeyEncryptionKey));
        using var rsa = RSA.Create(options.KeySize);
        var publicKeyDer = rsa.ExportSubjectPublicKeyInfo();
        var kid = Base64UrlEncoder.Encode(SHA256.HashData(publicKeyDer))[..16];
        var now = DateTime.UtcNow;

        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintextBytes = Encoding.UTF8.GetBytes(Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()));
        var cipherText = new byte[plaintextBytes.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(encryptionKey, 16);
        aes.Encrypt(nonce, plaintextBytes, cipherText, tag);

        var protectedBytes = new byte[nonce.Length + tag.Length + cipherText.Length];
        Buffer.BlockCopy(nonce, 0, protectedBytes, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, protectedBytes, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherText, 0, protectedBytes, nonce.Length + tag.Length, cipherText.Length);

        dbContext.Add(new PersistedSigningKey
        {
            Kid = kid,
            EncryptedPrivateKey = Convert.ToBase64String(protectedBytes),
            PublicKey = Convert.ToBase64String(publicKeyDer),
            Status = PersistedSigningKeyStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        try
        {
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();

            var initialized = await dbContext.Set<PersistedSigningKey>()
                .AsNoTracking()
                .AnyAsync(key => key.Status == PersistedSigningKeyStatus.Active || key.Status == PersistedSigningKeyStatus.Retired)
                .ConfigureAwait(false);
            if (initialized)
            {
                return;
            }

            throw;
        }
    }
}
