using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure.Extension.Authentication;

public enum PersistedSigningKeyStatus
{
    Active = 1,
    Retired = 2,
    Revoked = 3,
    NextActive = 4
}

public sealed class PersistedSigningKey
{
    public string Kid { get; set; } = string.Empty;

    public string EncryptedPrivateKey { get; set; } = string.Empty;

    public string PublicKey { get; set; } = string.Empty;

    public PersistedSigningKeyStatus Status { get; set; } = PersistedSigningKeyStatus.Active;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? InvalidatedAtUtc { get; set; }
}

public sealed class PersistedSigningKeyModelCreatingContributor : INOFDbContextModelCreatingContributor
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PersistedSigningKey>(entity =>
        {
            entity.IsHostOnly();
            entity.ToTable(nameof(PersistedSigningKey));
            entity.HasKey(e => e.Kid);
            entity.HasIndex(e => new { e.Status, e.CreatedAtUtc });
            entity.HasIndex(e => new { e.Status, e.InvalidatedAtUtc });
            entity.Property(e => e.Kid).HasMaxLength(64).IsRequired();
            entity.Property(e => e.EncryptedPrivateKey).IsRequired();
            entity.Property(e => e.PublicKey).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
        });
    }
}
