using Microsoft.Extensions.Options;
using NOF.Abstraction;
using System.Net.Http.Json;
using System.Text.Json;

namespace NOF.Infrastructure;

/// <summary>
/// Default HTTP-based JWKS client that discovers the JWKS endpoint from OAuth authorization server metadata.
/// </summary>
public sealed class HttpJwksService(HttpClient httpClient, IOptions<AuthenticationResourceServerOptions> options) :
    IJwksService,
    IAuthorizationServerMetadataService
{
    public async Task<JwksDocument> GetJwksAsync(CancellationToken cancellationToken = default)
    {
        var metadata = await GetMetadataAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(metadata?.JwksUri))
        {
            return new JwksDocument();
        }

        var jwksEndpoint = ResolveEndpoint(BuildMetadataEndpoint(), metadata.JwksUri);
        return await httpClient
            .GetFromJsonAsync(jwksEndpoint, JsonSerializerOptions.NOF.GetRequiredTypeInfo<JwksDocument>(), cancellationToken)
            .ConfigureAwait(false)
            ?? new JwksDocument();
    }

    public async Task<OAuthAuthorizationServerMetadataDocument?> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        var metadataEndpoint = BuildMetadataEndpoint();
        var metadata = await httpClient
            .GetFromJsonAsync(
                metadataEndpoint,
                JsonSerializerOptions.NOF.GetRequiredTypeInfo<OAuthAuthorizationServerMetadataDocument>(),
                cancellationToken)
            .ConfigureAwait(false);
        var expectedIssuer = OAuthAuthorizationServerMetadataUris.NormalizeIssuer(options.Value.AuthorizationServer);
        if (!string.Equals(metadata?.Issuer, expectedIssuer, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Authentication resource server metadata issuer does not match the configured authorization server.");
        }

        return metadata;
    }

    private Uri BuildMetadataEndpoint()
        => OAuthAuthorizationServerMetadataUris.BuildMetadataEndpoint(
            options.Value.AuthorizationServer,
            options.Value.RequireHttpsMetadata);

    private static Uri ResolveEndpoint(Uri metadataEndpoint, string endpoint)
    {
        var uri = new Uri(endpoint, UriKind.RelativeOrAbsolute);
        return uri.IsAbsoluteUri
            ? uri
            : new Uri(metadataEndpoint, uri);
    }
}
