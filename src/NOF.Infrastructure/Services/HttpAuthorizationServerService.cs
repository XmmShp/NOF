using Microsoft.Extensions.Options;
using NOF.Hosting;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace NOF.Infrastructure;

/// <summary>
/// Default HTTP client for OAuth authorization server metadata, JWKS retrieval, and token exchange.
/// </summary>
public sealed class HttpAuthorizationServerService(
    HttpClient httpClient,
    IOptions<AuthenticationResourceServerOptions> options) :
    IJwksService,
    IAuthorizationServerMetadataService,
    IJwtTokenExchangeService,
    IClientCredentialsTokenService
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
        var configuredIssuer = OAuthAuthorizationServerMetadataUris.NormalizeIssuer(options.Value.AuthorizationServer);
        var metadata = await GetMetadataAsync(
            configuredIssuer,
            options.Value.RequireHttpsMetadata,
            cancellationToken).ConfigureAwait(false);
        if (!string.Equals(metadata?.Issuer, configuredIssuer, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Authentication resource server metadata issuer does not match the configured authorization server.");
        }

        return metadata;
    }

    public async ValueTask<string> ExchangeTokenAsync(string subjectToken, JwtPropagation propagation, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectToken);
        ArgumentNullException.ThrowIfNull(propagation);

        var tokenEndpoint = await ResolveTokenExchangeEndpointAsync(subjectToken, cancellationToken).ConfigureAwait(false);
        var actorToken = await GetTokenAsync(
            tokenEndpoint,
            CreateTokenExchangeClientCredentialsRequest(),
            cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(CreateTokenExchangeFormValues(subjectToken, actorToken.AccessToken))
        };
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Token exchange failed with status {(int)response.StatusCode}: {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("access_token", out var accessTokenElement)
            || accessTokenElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(accessTokenElement.GetString()))
        {
            throw new InvalidOperationException("Token exchange response does not contain a valid access_token.");
        }

        return accessTokenElement.GetString()!;
    }

    public async ValueTask<ClientCredentialsTokenResponse> GetTokenAsync(
        ClientCredentialsTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ClientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ClientSecret);

        var authorizationServer = ResolveAuthorizationServer();
        var tokenEndpoint = await ResolveTokenEndpointAsync(authorizationServer, cancellationToken).ConfigureAwait(false);
        return await GetTokenAsync(tokenEndpoint, request, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ClientCredentialsTokenResponse> GetTokenAsync(
        Uri tokenEndpoint,
        ClientCredentialsTokenRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(CreateClientCredentialsFormValues(request))
        };
        message.Headers.Authorization = CreateBasicAuthorizationHeader(request.ClientId, request.ClientSecret);

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Client credentials token request failed with status {(int)response.StatusCode}: {payload}");
        }

        var tokenResponse = JsonSerializer.Deserialize(
            payload,
            typeof(OAuthClientCredentialsTokenResponse),
            NOFAuthenticationJsonSerializerContext.Default) as OAuthClientCredentialsTokenResponse;
        if (string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
        {
            throw new InvalidOperationException("Client credentials token response does not contain a valid access_token.");
        }

        return new ClientCredentialsTokenResponse
        {
            AccessToken = tokenResponse.AccessToken,
            TokenType = string.IsNullOrWhiteSpace(tokenResponse.TokenType) ? "Bearer" : tokenResponse.TokenType,
            ExpiresIn = tokenResponse.ExpiresIn,
            Scope = tokenResponse.Scope
        };
    }

    private async ValueTask<Uri> ResolveTokenExchangeEndpointAsync(string subjectToken, CancellationToken cancellationToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(subjectToken);
        if (string.IsNullOrWhiteSpace(jwt.Issuer))
        {
            throw new InvalidOperationException("Token exchange requires subject_token to contain a valid issuer.");
        }

        var issuer = OAuthAuthorizationServerMetadataUris.NormalizeIssuer(jwt.Issuer);
        var requireHttps = string.Equals(new Uri(issuer, UriKind.Absolute).Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var metadata = await GetMetadataAsync(issuer, requireHttps, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(metadata?.TokenEndpoint))
        {
            return ResolveEndpoint(
                OAuthAuthorizationServerMetadataUris.BuildMetadataEndpoint(issuer, requireHttps),
                metadata.TokenEndpoint);
        }

        return new Uri($"{issuer}/token", UriKind.Absolute);
    }

    private async ValueTask<Uri> ResolveTokenEndpointAsync(string authorizationServer, CancellationToken cancellationToken)
    {
        var issuer = OAuthAuthorizationServerMetadataUris.NormalizeIssuer(authorizationServer);
        var requireHttps = string.Equals(new Uri(issuer, UriKind.Absolute).Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var metadata = await GetMetadataAsync(issuer, requireHttps, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(metadata?.Issuer)
            && !string.Equals(metadata.Issuer, issuer, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Authorization server metadata issuer does not match the requested authorization server.");
        }

        if (!string.IsNullOrWhiteSpace(metadata?.TokenEndpoint))
        {
            return ResolveEndpoint(
                OAuthAuthorizationServerMetadataUris.BuildMetadataEndpoint(issuer, requireHttps),
                metadata.TokenEndpoint);
        }

        return new Uri($"{issuer}/token", UriKind.Absolute);
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
            options.Value.AuthorizationServer,
            options.Value.RequireHttpsMetadata);

    private static IReadOnlyDictionary<string, string> CreateTokenExchangeFormValues(string subjectToken, string actorToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:token-exchange",
            ["subject_token"] = subjectToken,
            ["subject_token_type"] = "urn:ietf:params:oauth:token-type:access_token",
            ["actor_token"] = actorToken,
            ["actor_token_type"] = "urn:ietf:params:oauth:token-type:access_token",
            ["requested_token_type"] = "urn:ietf:params:oauth:token-type:access_token"
        };

        return values;
    }

    private static IReadOnlyDictionary<string, string> CreateClientCredentialsFormValues(ClientCredentialsTokenRequest request)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "client_credentials"
        };

        if (!string.IsNullOrWhiteSpace(request.Scope))
        {
            values["scope"] = request.Scope;
        }

        return values;
    }

    private string ResolveAuthorizationServer()
    {
        if (!string.IsNullOrWhiteSpace(options.Value.AuthorizationServer))
        {
            return options.Value.AuthorizationServer;
        }

        throw new InvalidOperationException("AuthorizationServer must be configured in AuthenticationResourceServerOptions.");
    }

    private ClientCredentialsTokenRequest CreateTokenExchangeClientCredentialsRequest()
    {
        var client = options.Value.TokenExchangeClient;
        if (client is null
            || string.IsNullOrWhiteSpace(client.ClientId)
            || string.IsNullOrWhiteSpace(client.ClientSecret))
        {
            throw new InvalidOperationException("TokenExchangeClient credentials must be configured in AuthenticationResourceServerOptions.");
        }

        return new ClientCredentialsTokenRequest
        {
            ClientId = client.ClientId,
            ClientSecret = client.ClientSecret
        };
    }

    private static AuthenticationHeaderValue CreateBasicAuthorizationHeader(string clientId, string clientSecret)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }

    private static Uri ResolveEndpoint(Uri metadataEndpoint, string endpoint)
    {
        var uri = new Uri(endpoint, UriKind.RelativeOrAbsolute);
        return uri.IsAbsoluteUri
            ? uri
            : new Uri(metadataEndpoint, uri);
    }
}
