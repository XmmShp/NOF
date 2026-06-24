using NOF.Infrastructure;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class OAuthClient
{
    public string ClientId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string SecretHash { get; set; } = string.Empty;

    public string SecretSalt { get; set; } = string.Empty;

    public string AllowedScopes { get; set; } = "[]";

    public string AccessTokenClaims { get; set; } = "[]";

    public OAuthClientType ClientType { get; set; } = OAuthClientType.Confidential;

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class OAuthClientModelCreatingContributor : IDbContextModelCreatingContributor
{
    public void Configure(IDbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OAuthClient>(entity =>
        {
            entity.IsHostOnly();
            entity.ToTable(nameof(OAuthClient));
            entity.HasKey(e => e.ClientId);
            entity.HasIndex(e => e.IsEnabled);
            entity.Property(e => e.ClientId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.SecretHash).HasMaxLength(128).IsRequired();
            entity.Property(e => e.SecretSalt).HasMaxLength(64).IsRequired();
            entity.Property(e => e.AllowedScopes).IsRequired();
            entity.Property(e => e.AccessTokenClaims).IsRequired();
            entity.Property(e => e.ClientType).IsRequired();
            entity.Property(e => e.IsEnabled).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
        });
    }
}
