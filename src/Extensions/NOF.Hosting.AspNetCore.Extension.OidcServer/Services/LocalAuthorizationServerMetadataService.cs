using Microsoft.Extensions.Options;
using NOF.Infrastructure;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

/// <summary>
/// Provides authorization-server metadata directly from the co-located OIDC server configuration.
/// </summary>
public sealed class LocalAuthorizationServerMetadataService(
    IOptions<OAuthAuthorizationServerOptions> options) : IAuthorizationServerMetadataService
{
    public Task<OAuthAuthorizationServerMetadataDocument?> GetMetadataAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var issuer = OAuthAuthorizationServerMetadataUris.NormalizeIssuer(options.Value.Issuer);
        return Task.FromResult<OAuthAuthorizationServerMetadataDocument?>(new OAuthAuthorizationServerMetadataDocument
        {
            Issuer = issuer,
            TokenEndpoint = $"{issuer}/token",
            IntrospectionEndpoint = $"{issuer}/introspect",
            JwksUri = $"{issuer}/.well-known/jwks.json"
        });
    }
}
