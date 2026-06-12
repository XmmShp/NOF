using NOF.Hosting.AspNetCore.Extension.OidcServer;
using NOF.Infrastructure;
using NOF.Test;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace NOF.Infrastructure.Tests.Authentication.HttpClients;

public sealed class JwtJwksHttpClientTests
{
    [Fact]
    public async Task GetJwksAsync_ShouldUseConfiguredEndpoint()
    {
        var expected = new JwksDocument
        {
            Keys =
            [
                new JwkKeyDocument
                {
                    Kid = "kid-1",
                    Kty = "RSA",
                    Use = "sig",
                    N = "abc",
                    E = "AQAB"
                }
            ]
        };

        var handler = new CaptureHttpMessageHandler((_, _) => CreateJsonResponse(expected));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://auth.local")
        };

        var builder = NOFTestAppBuilder.Create();
        builder.AddAuthenticationResourceServer(options =>
        {
            options.JwksEndpoint = "https://auth.local/.well-known/jwks.json";
        });

        var service = new HttpJwksService(httpClient, Microsoft.Extensions.Options.Options.Create(new AuthenticationResourceServerOptions
        {
            JwksEndpoint = "https://auth.local/.well-known/jwks.json"
        }));

        var result = await service.GetJwksAsync();
        Assert.Single(result.Keys, k => k.Kid == "kid-1");
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal("/.well-known/jwks.json", handler.LastRequest.PathAndQuery);
        Assert.Empty(handler.LastRequest.Headers);
    }

    private static HttpResponseMessage CreateJsonResponse<T>(T payload)
    {
        Assert.IsType<JwksDocument>(payload);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"keys":[{"kid":"kid-1","kty":"RSA","use":"sig","n":"abc","e":"AQAB"}]}""",
                Encoding.UTF8,
                new MediaTypeHeaderValue("application/json"))
        };
    }

    private sealed class CaptureHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public CapturedRequest? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = new CapturedRequest
            {
                Method = request.Method,
                PathAndQuery = request.RequestUri?.PathAndQuery ?? string.Empty,
                Body = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken),
                Headers = request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.OrdinalIgnoreCase)
            };

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
