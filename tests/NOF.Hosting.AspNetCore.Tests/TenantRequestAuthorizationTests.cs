using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace NOF.Hosting.AspNetCore.Tests;

public sealed class TenantRequestAuthorizationTests
{
    [Theory]
    [InlineData(null, "forged", false, "/tenant", false, 401, null)]
    [InlineData(null, "forged", true, "/tenant", false, 403, null)]
    [InlineData("host", "forged", true, "/tenant", false, 403, null)]
    [InlineData("tenanta", "tenantb", true, "/tenant", false, 403, null)]
    [InlineData("unknown", "unknown", true, "/tenant", false, 403, null)]
    [InlineData("tenanta", null, true, "/tenant", false, 200, "tenanta")]
    [InlineData("tenanta", "tenanta", true, "/tenant", false, 200, "tenanta")]
    [InlineData(null, "host", true, "/host", false, 403, null)]
    [InlineData(null, "host", true, "/host", true, 200, "host")]
    [InlineData(null, null, false, "/stream", false, 401, null)]
    public async Task HttpRequests_ShouldAuthorizeBeforeResolvingTenantServices(
        string? identityTenant, string? headerTenant, bool sendToken, string path,
        bool platformPermission, int expectedStatus, string? expectedTenant)
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "test-key" };
        var builder = NOFWebApplicationBuilder.Create([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.Services.AddAuthenticationResourceServer(options =>
        {
            options.AuthorizationServerIssuer = "https://identity.local";
            options.ExpectedIssuer = "https://identity.local";
            options.Audience = "tenant-api";
        });
        builder.Services.AddSingleton<IJwksService>(new TestJwksService(rsa.ExportParameters(false)));
        builder.Services.Configure<TenantAuthorizationOptions>(options =>
        {
            options.RequireAuthenticatedTenantForRequests = true;
            options.RejectMismatchedTenantForRequests = true;
        });
        builder.Services.AddScoped<DefaultInboundAuthorizationHandler>();
        builder.Services.AddScoped<IInboundAuthorizationHandler, CatalogAuthorizationHandler>();
        var resolvedTenants = new List<string>();
        builder.Services.AddTransient(_ =>
        {
            // Represents tenant-specific service/connection resolution, before HandleAsync.
            resolvedTenants.Add(Context.Current.TenantId);
            return new TenantHandler();
        });
        builder.Services.AddTransient<StreamHandler>();
        builder.AddRpcServer<TenantServer>();
        await using var app = await builder.BuildAsync();
        app.MapOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();
        if (sendToken)
        {
            var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, "user") };
            if (identityTenant is not null)
            {
                claims.Add(new Claim(ClaimTypes.TenantId, identityTenant));
            }
            if (platformPermission)
            {
                claims.Add(new Claim(ClaimTypes.Permission, "platform.manage"));
            }
            var token = new JwtSecurityToken("https://identity.local", "tenant-api", claims,
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
                new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        if (headerTenant is not null)
        {
            client.DefaultRequestHeaders.Add(NOFAbstractionConstants.Transport.Headers.TenantId, headerTenant);
        }

        using var response = await client.PostAsJsonAsync(path, new Empty());
        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        if (expectedTenant is null)
        {
            Assert.Empty(resolvedTenants);
            Assert.False((await response.Content.ReadFromJsonAsync<Result>())!.IsSuccess);
        }
        else
        {
            Assert.Equal(expectedTenant, Assert.Single(resolvedTenants));
            var result = await response.Content.ReadFromJsonAsync<Result<string>>();
            Assert.True(result!.IsSuccess);
            Assert.Equal(expectedTenant, result.Value);
        }

        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText == path);
        var responses = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();
        Assert.Contains(responses, metadata => metadata.StatusCode == 401 && metadata.ContentTypes.Contains("application/json"));
        Assert.Contains(responses, metadata => metadata.StatusCode == 403 && metadata.ContentTypes.Contains("application/json"));

        using var openApi = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var documentedResponses = openApi.RootElement.GetProperty("paths").GetProperty(path)
            .GetProperty("post").GetProperty("responses");
        Assert.True(documentedResponses.GetProperty("401").GetProperty("content").TryGetProperty("application/json", out _));
        Assert.True(documentedResponses.GetProperty("403").GetProperty("content").TryGetProperty("application/json", out _));
    }

    [TransportOverHttp(HttpRpcStyle.ControllerRpc)]
    public interface ITenantService : IRpcService
    {
        [RequirePermission]
        [HttpEndpoint(HttpVerb.Post, "/tenant")]
        Result<string> Tenant(Empty request);

        [UseHostTenant]
        [RequirePermission("platform.manage")]
        [HttpEndpoint(HttpVerb.Post, "/host")]
        Result<string> Host(Empty request);

        [RequirePermission]
        [HttpEndpoint(HttpVerb.Post, "/stream")]
        StreamingResult<string> Stream(Empty request);
    }

    public sealed class TenantServer : RpcServer<ITenantService>, IRpcServer
    {
        public static IReadOnlyDictionary<string, RpcHandlerMapping> HandlerMappings { get; } =
            new Dictionary<string, RpcHandlerMapping>
            {
                [nameof(ITenantService.Tenant)] = new(typeof(TenantHandler), typeof(Empty), typeof(Result<string>)),
                [nameof(ITenantService.Host)] = new(typeof(TenantHandler), typeof(Empty), typeof(Result<string>)),
                [nameof(ITenantService.Stream)] = new(typeof(StreamHandler), typeof(Empty), typeof(StreamingResult<string>))
            };

        protected override IReadOnlyDictionary<string, RpcHandlerMapping> GetHandlerMappings() => HandlerMappings;
    }

    public sealed class TenantHandler : RpcHandler<Empty, Result<string>>
    {
        public override Task<Result<string>> HandleAsync(Empty request, Context context, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success(context.TenantId));
    }

    public sealed class StreamHandler : RpcHandler<Empty, StreamingResult<string>>
    {
        public override Task<StreamingResult<string>> HandleAsync(Empty request, Context context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Unauthorized streaming handlers must not run");
    }

    public sealed class CatalogAuthorizationHandler(DefaultInboundAuthorizationHandler defaultHandler) : IInboundAuthorizationHandler
    {
        public async ValueTask<InboundAuthorizationResult> AuthorizeAsync(InboundAuthorizationContext context, CancellationToken cancellationToken)
        {
            var result = await defaultHandler.AuthorizeAsync(context, cancellationToken);
            if (result is InboundAuthorizationResult.Denied || !context.User.IsAuthenticated
                || UseHostTenantAttribute.IsRequired(context.ServiceMethodInfo))
            {
                return result;
            }
            // Application-owned catalog/readiness check uses trusted identity, before service resolution.
            return context.User.TenantId == "tenanta"
                ? result
                : new InboundAuthorizationResult.Denied(Result.Fail("403", "Tenant is not available"), 403, []);
        }
    }

    public sealed class TestJwksService(RSAParameters parameters) : IJwksService
    {
        public Task<JwksDocument> GetJwksAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new JwksDocument
            {
                Keys = [new JwkKeyDocument
                {
                    Kid = "test-key", Kty = "RSA", Use = "sig", Alg = SecurityAlgorithms.RsaSha256,
                    N = Base64UrlEncoder.Encode(parameters.Modulus!), E = Base64UrlEncoder.Encode(parameters.Exponent!)
                }]
            });
    }
}
