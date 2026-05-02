using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed class JwtAuthorizationDbContextModelCreatingContributor : INOFDbContextModelCreatingContributor
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PersistedSigningKey>(entity =>
        {
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
            entity.Property(e => e.ConcurrencyStamp).HasMaxLength(64).IsConcurrencyToken().IsRequired();
        });

        modelBuilder.Entity<RevokedRefreshToken>(entity =>
        {
            entity.ToTable(nameof(RevokedRefreshToken));
            entity.HasKey(e => e.TokenId);
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.TokenId).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
        });
    }
}
