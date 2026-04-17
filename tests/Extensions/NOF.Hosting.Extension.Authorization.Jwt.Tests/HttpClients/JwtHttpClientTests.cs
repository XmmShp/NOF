using Microsoft.IdentityModel.Tokens;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Contract.Extension.Authorization.Jwt;
using NOF.Hosting;
using NOF.Infrastructure.Extension.Authorization.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace NOF.Hosting.Extension.Authorization.Jwt.Tests.HttpClients;

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
        var service = new HttpJwksService(httpClient, pipeline, new SimpleServiceProvider());

        var result = await service.GetJwksAsync(new GetJwksRequest());
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value!.Keys, k => k.Kid == "kid-1");
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal(JwtAuthorizationEndpoints.Jwks, handler.LastRequest.PathAndQuery);
        Assert.True(handler.LastRequest.Headers.ContainsKey("X-Tenant"));
        Assert.Single(handler.LastRequest.Headers["X-Tenant"]);
        Assert.Equal("tenanta", handler.LastRequest.Headers["X-Tenant"][0]);
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

        var pipeline = new CapturingOutboundPipelineExecutor(new Dictionary<string, string?>
        {
            ["Authorization"] = "Bearer upstream-token"
        });
        var service = new HttpJwtAuthorityService(httpClient, pipeline, new SimpleServiceProvider());

        var request = new GenerateJwtTokenRequest
        {
            UserId = "user-1",
            TenantId = "tenanta",
            Audience = "orders-api",
            AccessTokenExpiration = TimeSpan.FromMinutes(10),
            RefreshTokenExpiration = TimeSpan.FromDays(7),
            Permissions = ["orders.read"],
            CustomClaims = new Dictionary<string, string> { ["role"] = "admin" }
        };

        var result = await service.GenerateJwtTokenAsync(request);
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

    private sealed class CapturingOutboundPipelineExecutor : IRequestOutboundPipelineExecutor
    {
        private readonly IReadOnlyDictionary<string, string?> _headers;

        public CapturingOutboundPipelineExecutor()
            : this(new Dictionary<string, string?>
            {
                ["X-Tenant"] = "tenanta",
                ["X-Trace"] = "trace-a"
            })
        {
        }

        public CapturingOutboundPipelineExecutor(IReadOnlyDictionary<string, string?> headers)
        {
            _headers = headers;
        }

        public RequestOutboundContext? LastContext { get; private set; }

        public async ValueTask ExecuteAsync(
            RequestOutboundContext context,
            HandlerDelegate dispatch,
            CancellationToken cancellationToken)
        {
            foreach (var (key, value) in _headers)
            {
                context.Headers[key] = value;
            }

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
