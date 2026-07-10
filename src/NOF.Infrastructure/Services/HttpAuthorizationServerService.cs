using Microsoft.Extensions.Options;
using NOF.Hosting;
using System.Net.Http.Json;

namespace NOF.Infrastructure;

/// <summary>
/// Default HTTP client for OAuth authorization server metadata and JWKS retrieval.
/// </summary>
public sealed class HttpAuthorizationServerService(
    HttpClient httpClient,
    IOptions<AuthenticationResourceServerOptions> options) :
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

        var jwksEndpoint = ResolveEndpoint(BuildConfiguredMetadataEndpoint(), metadata.JwksUri);
        return await httpClient
            .GetFromJsonAsync(jwksEndpoint, NOFAuthenticationJsonSerializerContext.Default.JwksDocument, cancellationToken)
            .ConfigureAwait(false)
            ?? new JwksDocument();
    }

    public async Task<OAuthAuthorizationServerMetadataDocument?> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        var configuredIssuer = OAuthAuthorizationServerMetadataUris.NormalizeIssuer(options.Value.AuthorizationServerIssuer);
        var metadata = await GetMetadataAsync(
            configuredIssuer,
            options.Value.RequireHttpsMetadata,
            cancellationToken).ConfigureAwait(false);
        if (!string.Equals(metadata?.Issuer, configuredIssuer, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Authentication resource server metadata issuer does not match the configured authorization server issuer.");
        }

        return metadata;
    }

    private Task<OAuthAuthorizationServerMetadataDocument?> GetMetadataAsync(
        string issuer,
        bool requireHttps,
        CancellationToken cancellationToken)
    {
        var metadataEndpoint = OAuthAuthorizationServerMetadataUris.BuildMetadataEndpoint(issuer, requireHttps);
        return httpClient.GetFromJsonAsync(
            metadataEndpoint,
            NOFAuthenticationJsonSerializerContext.Default.OAuthAuthorizationServerMetadataDocument,
            cancellationToken);
    }

    private Uri BuildConfiguredMetadataEndpoint()
        => OAuthAuthorizationServerMetadataUris.BuildMetadataEndpoint(
            options.Value.AuthorizationServerIssuer,
            options.Value.RequireHttpsMetadata);

    private static Uri ResolveEndpoint(Uri metadataEndpoint, string endpoint)
    {
        var uri = new Uri(endpoint, UriKind.RelativeOrAbsolute);
        return uri.IsAbsoluteUri
            ? uri
            : new Uri(metadataEndpoint, uri);
    }
}
