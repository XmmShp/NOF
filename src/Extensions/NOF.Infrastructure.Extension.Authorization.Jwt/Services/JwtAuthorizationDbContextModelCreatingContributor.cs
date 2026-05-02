using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed class JwtAuthorizationDbContextModelCreatingContributor : INOFDbContextModelCreatingContributor
{
    public void Configure(ModelBuilder modelBuilder)
    {
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
