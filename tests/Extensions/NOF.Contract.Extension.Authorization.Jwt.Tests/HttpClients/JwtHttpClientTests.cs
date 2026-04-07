using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using NOF.Contract;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

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
        var executionContext = new NOF.Contract.ExecutionContext
        {
            ["X-Tenant"] = "tenant-a",
            ["X-Trace"] = "trace-a"
        };
        var service = new HttpJwksService(httpClient, pipeline, executionContext, new SimpleServiceProvider());

        var result = await service.GetJwksAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Keys.Should().ContainSingle(k => k.Kid == "kid-1");

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.PathAndQuery.Should().Be(JwtAuthorizationEndpoints.Jwks);
        handler.LastRequest.Headers.Should().ContainKey("X-Tenant");
        handler.LastRequest.Headers["X-Tenant"].Should().ContainSingle("tenant-a");

        pipeline.LastContext.Should().NotBeNull();
        pipeline.LastContext!.Response.Should().BeAssignableTo<Result<JwksDocument>>();
    }

    [Fact]
    public async Task GenerateJwtTokenAsync_ShouldPostExpectedPayload_AndReturnResult()
    {
        var expectedResponse = Result.Success(new GenerateJwtTokenResponse(new TokenPair
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(10),
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7)
        }));

        var handler = new CaptureHttpMessageHandler((_, _) => CreateJsonResponse(expectedResponse));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://auth.local")
        };

        var pipeline = new CapturingOutboundPipelineExecutor();
        var executionContext = new NOF.Contract.ExecutionContext
        {
            ["Authorization"] = "Bearer upstream-token"
        };
        var service = new HttpJwtAuthorityService(httpClient, pipeline, executionContext, new SimpleServiceProvider());

        var request = new GenerateJwtTokenRequest(
            UserId: "user-1",
            TenantId: "tenant-a",
            Audience: "orders-api",
            AccessTokenExpiration: TimeSpan.FromMinutes(10),
            RefreshTokenExpiration: TimeSpan.FromDays(7),
            Permissions: ["orders.read"],
            CustomClaims: new Dictionary<string, string> { ["role"] = "admin" });

        var result = await service.GenerateJwtTokenAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TokenPair.AccessToken.Should().Be("access-token");

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.PathAndQuery.Should().Be(JwtAuthorizationEndpoints.Token);
        handler.LastRequest.Headers.Should().ContainKey("Authorization");

        var payload = JsonSerializer.Deserialize<GenerateJwtTokenRequest>(handler.LastRequest.Body!, JsonSerializerOptions.NOF);
        payload.Should().NotBeNull();
        payload!.UserId.Should().Be("user-1");
        payload.Permissions.Should().ContainSingle("orders.read");

        pipeline.LastContext.Should().NotBeNull();
        pipeline.LastContext!.Message.Should().BeOfType<GenerateJwtTokenRequest>();
        pipeline.LastContext.Response.Should().BeAssignableTo<Result<GenerateJwtTokenResponse>>();
    }

    [Fact]
    public void JwtAuthorizationEndpoints_ShouldMatchWellKnownRoutes()
    {
        JwtAuthorizationEndpoints.Jwks.Should().Be("/.well-known/jwks.json");
        JwtAuthorizationEndpoints.Token.Should().Be("/connect/token");
        JwtAuthorizationEndpoints.Introspect.Should().Be("/connect/introspect");
        JwtAuthorizationEndpoints.Revocation.Should().Be("/connect/revocation");
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
