using NOF.Infrastructure;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class OAuthClient
{
    public string ClientId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string SecretHash { get; set; } = string.Empty;

    public string SecretSalt { get; set; } = string.Empty;

    public string JsonWebKeySet { get; set; } = string.Empty;

    public string AllowedScopes { get; set; } = "[]";

    public string RedirectUris { get; set; } = "[]";

    public string AccessTokenClaims { get; set; } = "[]";

    public string TokenEndpointAuthenticationMethod { get; set; } = string.Empty;

    public string AllowedGrantTypes { get; set; } = "[]";

    public string AllowedResponseTypes { get; set; } = "[]";

    public string ApplicationType { get; set; } = OAuthClientApplicationTypes.Web;

    public string RegistrationMetadata { get; set; } = "{}";

    public string RegistrationAccessTokenHash { get; set; } = string.Empty;

    public string RegistrationAccessTokenSalt { get; set; } = string.Empty;

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
            entity.Property(e => e.JsonWebKeySet).IsRequired();
            entity.Property(e => e.AllowedScopes).IsRequired();
            entity.Property(e => e.RedirectUris).IsRequired();
            entity.Property(e => e.AccessTokenClaims).IsRequired();
            entity.Property(e => e.TokenEndpointAuthenticationMethod).HasMaxLength(64).IsRequired();
            entity.Property(e => e.AllowedGrantTypes).IsRequired();
            entity.Property(e => e.AllowedResponseTypes).IsRequired();
            entity.Property(e => e.ApplicationType).HasMaxLength(16).IsRequired();
            entity.Property(e => e.RegistrationMetadata).IsRequired();
            entity.Property(e => e.RegistrationAccessTokenHash).HasMaxLength(128).IsRequired();
            entity.Property(e => e.RegistrationAccessTokenSalt).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ClientType).IsRequired();
            entity.Property(e => e.IsEnabled).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
        });
    }
}
