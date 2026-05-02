using NOF.Infrastructure;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

[HostOnly]
public sealed class PersistedSigningKey
{
    public string Kid { get; set; } = string.Empty;

    public string EncryptedPrivateKey { get; set; } = string.Empty;

    public string PublicKey { get; set; } = string.Empty;

    public PersistedSigningKeyStatus Status { get; set; } = PersistedSigningKeyStatus.Active;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? InvalidatedAtUtc { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;
}
