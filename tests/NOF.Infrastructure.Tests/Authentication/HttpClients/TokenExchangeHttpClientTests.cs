using NOF.Hosting;
using NOF.Test;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace NOF.Infrastructure.Tests.Authentication.HttpClients;

public sealed class TokenExchangeHttpClientTests
{
    [Fact]
    public async Task ExchangeTokenAsync_ShouldUseServiceAccessTokenAsActorToken()
    {
        var requestIndex = 0;
        var handler = new CaptureHttpMessageHandler((request, cancellationToken) =>
        {
            _ = cancellationToken;
            requestIndex++;
            return request.RequestUri?.PathAndQuery switch
            {
                "/.well-known/oauth-authorization-server/oauth2" => CreateJsonResponse(
                    """{"issuer":"https://auth.local/oauth2","token_endpoint":"https://auth.local/oauth2/token","jwks_uri":"https://auth.local/oauth2/.well-known/jwks.json"}"""),
                "/oauth2/token" when requestIndex == 2 => CreateJsonResponse(
                    """{"access_token":"service-token","token_type":"Bearer","scope":"orders-api"}"""),
                "/oauth2/token" when requestIndex == 3 => CreateJsonResponse("""{"access_token":"exchanged-token"}"""),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://auth.local")
        };
        var service = new HttpAuthorizationServerService(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(new AuthenticationResourceServerOptions
            {
                AuthorizationServer = "https://auth.local/oauth2",
                TokenExchangeClient = new AuthenticationClientCredentialsOptions
                {
                    ClientId = "orders-api",
                    ClientSecret = "orders-secret"
                }
            }));
        var subjectToken = CreateUnsignedToken("https://auth.local/oauth2");

        var result = await service.ExchangeTokenAsync(subjectToken, new JwtPropagation { EnableTokenExchange = true }, default);

        Assert.Equal("exchanged-token", result);
        Assert.Equal(
            ["/.well-known/oauth-authorization-server/oauth2", "/oauth2/token", "/oauth2/token"],
            handler.Requests.Select(static request => request.PathAndQuery).ToArray());
        Assert.Equal("grant_type=client_credentials", handler.Requests[1].Body);
        Assert.Equal(
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("orders-api:orders-secret")),
            handler.Requests[1].Headers["Authorization"].Single());
        Assert.Equal(
            "grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Atoken-exchange&subject_token="
            + Uri.EscapeDataString(subjectToken)
            + "&subject_token_type=urn%3Aietf%3Aparams%3Aoauth%3Atoken-type%3Aaccess_token&actor_token=service-token&actor_token_type=urn%3Aietf%3Aparams%3Aoauth%3Atoken-type%3Aaccess_token&requested_token_type=urn%3Aietf%3Aparams%3Aoauth%3Atoken-type%3Aaccess_token",
            handler.Requests[2].Body);
    }

    [Fact]
    public async Task GetTokenAsync_ShouldUseClientCredentialsGrantAndBasicAuthentication()
    {
        var handler = new CaptureHttpMessageHandler(static (request, _) => request.RequestUri?.PathAndQuery switch
        {
            "/.well-known/oauth-authorization-server/oauth2" => CreateJsonResponse(
                """{"issuer":"https://auth.local/oauth2","token_endpoint":"https://auth.local/oauth2/token","jwks_uri":"https://auth.local/oauth2/.well-known/jwks.json"}"""),
            "/oauth2/token" => CreateJsonResponse("""{"access_token":"service-token","token_type":"Bearer","expires_in":900,"scope":"orders.read"}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://auth.local")
        };
        var service = new HttpAuthorizationServerService(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(new AuthenticationResourceServerOptions
            {
                AuthorizationServer = "https://auth.local/oauth2"
            }));

        var result = await service.GetTokenAsync(new ClientCredentialsTokenRequest
        {
            ClientId = "orders-api",
            ClientSecret = "orders-secret",
            Scope = "orders.read"
        });

        Assert.Equal("service-token", result.AccessToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(900, result.ExpiresIn);
        Assert.Equal("orders.read", result.Scope);
        Assert.Equal(
            ["/.well-known/oauth-authorization-server/oauth2", "/oauth2/token"],
            handler.Requests.Select(static request => request.PathAndQuery).ToArray());
        Assert.Equal("grant_type=client_credentials&scope=orders.read", handler.Requests[1].Body);
        Assert.Equal(
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("orders-api:orders-secret")),
            handler.Requests[1].Headers["Authorization"].Single());
    }

    private static string CreateUnsignedToken(string issuer)
    {
        var header = Base64UrlEncode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncode($$"""{"sub":"user-1","iss":"{{issuer}}"}""");
        return header + "." + payload + ".";
    }

    private static string Base64UrlEncode(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };
    }
    private sealed class CaptureHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest
            {
                Method = request.Method,
                PathAndQuery = request.RequestUri?.PathAndQuery ?? string.Empty,
                Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
                Headers = request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.OrdinalIgnoreCase)
            });

            return responseFactory(request, cancellationToken);
        }
    }

    private sealed class CapturedRequest
    {
        public required HttpMethod Method { get; init; }
        public required string PathAndQuery { get; init; }
        public required Dictionary<string, string[]> Headers { get; init; }
        public string? Body { get; init; }
    }
}
