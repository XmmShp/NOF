using Microsoft.IdentityModel.Tokens;
using NOF.Abstraction;
using NOF.Hosting;
using NOF.Infrastructure.Extension.Authorization.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;
using ExecutionContext = NOF.Application.ExecutionContext;

namespace NOF.Contract.Extension.Authorization.Jwt.Tests.HttpClients;

public sealed class JwtHttpClientTests
{
    [Fact]
    public async Task GetJwksAsync_ShouldUseConfiguredEndpoint_PropagateHeaders_AndSetPipelineResponse()
    {
        var expected = Result.Success(new JwksDocument
        {
            Keys =
            [
                new JsonWebKey
                {
                    Kid = "kid-1",
                    Kty = "RSA",
                    Use = "sig",
                    N = "abc",
                    E = "AQAB"
                }
            ]
        });

        var handler = new CaptureHttpMessageHandler((_, _) => CreateJsonResponse(expected));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://auth.local")
        };

        var pipeline = new CapturingOutboundPipelineExecutor();
        var executionContext = new ExecutionContext
        {
            ["X-Tenant"] = "tenant-a",
            ["X-Trace"] = "trace-a"
        };
        var service = new HttpJwksService(httpClient, pipeline, executionContext, new SimpleServiceProvider());

        var result = await service.GetJwksAsync(default);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value!.Keys, k => k.Kid == "kid-1");
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal(JwtAuthorizationEndpoints.Jwks, handler.LastRequest.PathAndQuery);
        Assert.True(handler.LastRequest.Headers.ContainsKey("X-Tenant"));
        Assert.Single(handler.LastRequest.Headers["X-Tenant"]);
        Assert.Equal("tenant-a", handler.LastRequest.Headers["X-Tenant"][0]);
        Assert.NotNull(pipeline.LastContext);
        Assert.IsAssignableFrom<Result<JwksDocument>>(pipeline.LastContext!.Response);
    }

    [Fact]
    public async Task GenerateJwtTokenAsync_ShouldPostExpectedPayload_AndReturnResult()
    {
        var expectedResponse = Result.Success(new GenerateJwtTokenResponse
        {
            TokenPair = new TokenPair
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(10),
                RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7)
            }
        });

        var handler = new CaptureHttpMessageHandler((_, _) => CreateJsonResponse(expectedResponse));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://auth.local")
        };

        var pipeline = new CapturingOutboundPipelineExecutor();
        var executionContext = new ExecutionContext
        {
            ["Authorization"] = "Bearer upstream-token"
        };
        var service = new HttpJwtAuthorityService(httpClient, pipeline, executionContext, new SimpleServiceProvider());

        var request = new GenerateJwtTokenRequest
        {
            UserId = "user-1",
            TenantId = "tenant-a",
            Audience = "orders-api",
            AccessTokenExpiration = TimeSpan.FromMinutes(10),
            RefreshTokenExpiration = TimeSpan.FromDays(7),
            Permissions = ["orders.read"],
            CustomClaims = new Dictionary<string, string> { ["role"] = "admin" }
        };

        var result = await service.GenerateJwtTokenAsync(request, default);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("access-token", result.Value!.TokenPair.AccessToken);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(JwtAuthorizationEndpoints.Token, handler.LastRequest.PathAndQuery);
        Assert.True(handler.LastRequest.Headers.ContainsKey("Authorization"));

        var payload = JsonSerializer.Deserialize<GenerateJwtTokenRequest>(handler.LastRequest.Body!, JsonSerializerOptions.NOF);
        Assert.NotNull(payload);
        Assert.Equal("user-1", payload!.UserId);
        Assert.Single(payload.Permissions!);
        Assert.Equal("orders.read", payload.Permissions![0]);
        Assert.NotNull(pipeline.LastContext);
        Assert.IsType<GenerateJwtTokenRequest>(pipeline.LastContext!.Message);
        Assert.IsType<Result<GenerateJwtTokenResponse>>(pipeline.LastContext.Response, exactMatch: true);
    }

    [Fact]
    public void JwtAuthorizationEndpoints_ShouldMatchWellKnownRoutes()
    {
        Assert.Equal("/.well-known/jwks.json", JwtAuthorizationEndpoints.Jwks);
        Assert.Equal("/connect/token", JwtAuthorizationEndpoints.Token);
        Assert.Equal("/connect/introspect", JwtAuthorizationEndpoints.Introspect);
        Assert.Equal("/connect/revocation", JwtAuthorizationEndpoints.Revocation);
    }

    private static HttpResponseMessage CreateJsonResponse<T>(T payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonSerializerOptions.NOF),
                Encoding.UTF8,
                new MediaTypeHeaderValue("application/json"))
        };
    }

    private sealed class CapturingOutboundPipelineExecutor : IOutboundPipelineExecutor
    {
        public OutboundContext? LastContext { get; private set; }

        public async ValueTask ExecuteAsync(OutboundContext context, OutboundDelegate dispatch, CancellationToken cancellationToken)
        {
            LastContext = context;
            await dispatch(cancellationToken);
        }
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

    private sealed class SimpleServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
