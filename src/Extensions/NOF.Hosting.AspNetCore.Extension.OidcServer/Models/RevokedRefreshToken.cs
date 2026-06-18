using NOF.Infrastructure;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class RevokedRefreshToken
{
    public string TokenId { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class RevokedRefreshTokenModelCreatingContributor : IDbContextModelCreatingContributor
{
    public void Configure(IDbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RevokedRefreshToken>(entity =>
        {
            entity.IsHostOnly();
            entity.ToTable(nameof(RevokedRefreshToken));
            entity.HasKey(e => e.TokenId);
            entity.HasIndex(e => e.ExpiresAtUtc);
            entity.Property(e => e.TokenId).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ExpiresAtUtc).IsRequired();
        });
    }
}
