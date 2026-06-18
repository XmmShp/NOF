using NOF.Hosting;
using NOF.Test;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace NOF.Infrastructure.Tests.Authentication.HttpClients;

public sealed class JwtJwksHttpClientTests
{
    [Fact]
    public async Task GetJwksAsync_ShouldDiscoverJwksEndpointFromAuthorizationServerMetadata()
    {
        var handler = new CaptureHttpMessageHandler(static (request, _) => request.RequestUri?.PathAndQuery switch
        {
            "/.well-known/oauth-authorization-server/oauth2" => CreateJsonResponse(
                """{"issuer":"https://auth.local/oauth2","jwks_uri":"https://auth.local/oauth2/.well-known/jwks.json"}"""),
            "/oauth2/.well-known/jwks.json" => CreateJsonResponse(
                """{"keys":[{"kid":"kid-1","kty":"RSA","use":"sig","n":"abc","e":"AQAB"}]}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://auth.local")
        };

        var builder = NOFTestAppBuilder.Create();
        builder.AddAuthenticationResourceServer(options =>
        {
            options.AuthorizationServer = "https://auth.local/oauth2";
        });

        var service = new HttpJwksService(httpClient, Microsoft.Extensions.Options.Options.Create(new AuthenticationResourceServerOptions
        {
            AuthorizationServer = "https://auth.local/oauth2"
        }));

        var result = await service.GetJwksAsync();
        Assert.Single(result.Keys, k => k.Kid == "kid-1");
        Assert.Equal(
            ["/.well-known/oauth-authorization-server/oauth2", "/oauth2/.well-known/jwks.json"],
            handler.Requests.Select(request => request.PathAndQuery));
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Empty(request.Headers);
        });
    }

    [Fact]
    public async Task GetJwksAsync_ShouldRejectMetadataWhenIssuerDoesNotMatchAuthorizationServer()
    {
        var handler = new CaptureHttpMessageHandler(static (_, _) => CreateJsonResponse(
            """{"issuer":"https://other.local/oauth2","jwks_uri":"https://auth.local/oauth2/.well-known/jwks.json"}"""));
        var httpClient = new HttpClient(handler);
        var service = new HttpJwksService(httpClient, Microsoft.Extensions.Options.Options.Create(new AuthenticationResourceServerOptions
        {
            AuthorizationServer = "https://auth.local/oauth2"
        }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetJwksAsync());
        Assert.Equal("Authentication resource server metadata issuer does not match the configured authorization server.", exception.Message);
        Assert.Equal("/.well-known/oauth-authorization-server/oauth2", Assert.Single(handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task GetJwksAsync_ShouldRejectHttpMetadataWhenHttpsMetadataIsRequired()
    {
        var handler = new CaptureHttpMessageHandler(static (_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var service = new HttpJwksService(httpClient, Microsoft.Extensions.Options.Options.Create(new AuthenticationResourceServerOptions
        {
            AuthorizationServer = "http://auth.local",
            RequireHttpsMetadata = true
        }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetJwksAsync());
        Assert.Equal("Authentication resource server authorization server metadata must use HTTPS.", exception.Message);
        Assert.Empty(handler.Requests);
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                new MediaTypeHeaderValue("application/json"))
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
                Body = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken),
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
