using Microsoft.Extensions.Options;
using NOF.Contract;
using NOF.Hosting.AspNetCore.Extension.OidcServer;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NOF.Sample.Services;

public sealed class OAuthChainDemoBackend(
    HttpClient httpClient,
    IOAuthClientRepository clientRepository,
    IOptions<OAuthAuthorizationServerOptions> authorizationServerOptions)
{
    private static readonly string[] AllowedScopes = ["sample.read", "sample.write"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string Issuer => authorizationServerOptions.Value.Issuer.TrimEnd('/');
    private string TokenEndpoint => $"{Issuer}/token";
    private string AuthorizeEndpoint => $"{Issuer}/authorize";

    public async Task<Result<CreateDemoOAuthClientResponse>> CreateClientAsync(
        CreateDemoOAuthClientRequest request,
        CancellationToken cancellationToken)
    {
        var clientIdPrefix = string.IsNullOrWhiteSpace(request.ClientIdPrefix)
            ? "sample-client"
            : request.ClientIdPrefix.Trim().ToLowerInvariant();
        var clientId = $"{clientIdPrefix}-{Guid.NewGuid().ToString("N")[..8]}";
        var createResult = await clientRepository.CreateAsync(
            new CreateOAuthClientRequest
            {
                ClientId = clientId,
                DisplayName = $"Sample Demo Client {clientId}",
                AllowedScopes = AllowedScopes
            },
            cancellationToken).ConfigureAwait(false);

        if (!createResult.IsSuccess)
        {
            return Result.Fail(createResult.ErrorCode, createResult.Message);
        }

        if (string.IsNullOrWhiteSpace(createResult.Value.ClientSecret))
        {
            return Result.Fail("500", "Created OAuth client did not return a client secret.");
        }

        return new CreateDemoOAuthClientResponse
        {
            ClientId = createResult.Value.Client.ClientId,
            ClientSecret = createResult.Value.ClientSecret,
            AllowedScopes = AllowedScopes
        };
    }

    public async Task<Result<DemoTokenResponse>> GetClientTokenAsync(
        GetDemoClientTokenRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = string.Join(' ', AllowedScopes)
            })
        };
        message.Headers.Authorization = CreateBasicAuthorizationHeader(request.ClientId, request.ClientSecret);

        return await SendTokenRequestAsync(message, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<DemoTokenResponse>> GetUserTokenAsync(CancellationToken cancellationToken)
    {
        var codeVerifier = CreateCodeVerifier();
        var codeChallenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        var redirectUri = "https://client.local/oauth/callback";
        var authorizeUrl =
            $"{AuthorizeEndpoint}?response_type=code" +
            "&client_id=nof-sample-ui" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope={Uri.EscapeDataString("openid profile email sample.read sample.write")}" +
            $"&state={Uri.EscapeDataString($"state-{Guid.NewGuid().ToString("N")[..8]}")}" +
            $"&nonce={Uri.EscapeDataString($"nonce-{Guid.NewGuid().ToString("N")[..8]}")}" +
            $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
            "&code_challenge_method=S256";

        using var authorizeClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
        using var authorizeRequest = new HttpRequestMessage(HttpMethod.Get, authorizeUrl);
        using var authorizeResponse = await authorizeClient.SendAsync(
            authorizeRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (authorizeResponse.StatusCode != HttpStatusCode.Redirect
            && authorizeResponse.StatusCode != HttpStatusCode.Found)
        {
            return await CreateFailureAsync<DemoTokenResponse>(authorizeResponse, cancellationToken).ConfigureAwait(false);
        }

        var redirectLocation = authorizeResponse.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(redirectLocation))
        {
            return Result.Fail("500", "Authorization response did not contain redirect location.");
        }

        var code = GetRequiredQueryValue(redirectLocation, "code");
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = "nof-sample-ui",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier
            })
        };

        return await SendTokenRequestAsync(tokenRequest, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<DemoTokenResponse>> ExchangeTokenAsync(
        ExchangeDemoTokenRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:token-exchange",
                ["subject_token"] = request.UserAccessToken,
                ["subject_token_type"] = "urn:ietf:params:oauth:token-type:access_token",
                ["actor_token"] = request.ClientAccessToken,
                ["actor_token_type"] = "urn:ietf:params:oauth:token-type:access_token",
                ["requested_token_type"] = "urn:ietf:params:oauth:token-type:access_token",
                ["scope"] = request.RequestedScope
            })
        };
        message.Headers.Authorization = CreateBasicAuthorizationHeader(request.ClientId, request.ClientSecret);

        return await SendTokenRequestAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<DemoTokenResponse>> SendTokenRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return await CreateFailureAsync<DemoTokenResponse>(response, cancellationToken).ConfigureAwait(false);
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var token = JsonSerializer.Deserialize<OAuthTokenResponseDocument>(payload, JsonOptions);
        if (token?.AccessToken is null)
        {
            return Result.Fail("500", "Token response payload is invalid.");
        }

        return new DemoTokenResponse
        {
            AccessToken = token.AccessToken,
            TokenType = string.IsNullOrWhiteSpace(token.TokenType) ? "Bearer" : token.TokenType,
            ExpiresIn = token.ExpiresIn,
            Scope = token.Scope,
            IdToken = token.IdToken
        };
    }

    private static async Task<Result<T>> CreateFailureAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Result.Fail(((int)response.StatusCode).ToString(), string.IsNullOrWhiteSpace(payload) ? response.ReasonPhrase ?? "Request failed." : payload);
    }

    private static AuthenticationHeaderValue CreateBasicAuthorizationHeader(string clientId, string clientSecret)
        => new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));

    private static string CreateCodeVerifier()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Base64UrlEncode(buffer.ToArray());
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string GetRequiredQueryValue(string url, string key)
    {
        var query = new Uri(url, UriKind.Absolute).Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0], key, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        throw new InvalidOperationException($"Missing query parameter '{key}'.");
    }

    private sealed record OAuthTokenResponseDocument
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }

        [JsonPropertyName("expires_in")]
        public long? ExpiresIn { get; init; }

        [JsonPropertyName("scope")]
        public string? Scope { get; init; }

        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }
    }
}
