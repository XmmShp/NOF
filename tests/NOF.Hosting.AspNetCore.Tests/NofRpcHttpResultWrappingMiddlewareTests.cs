using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Xunit;

namespace NOF.Hosting.AspNetCore.Tests;

public sealed class HttpRpcTransportBoundaryTests
{
    [Fact]
    public async Task RpcHttpEndpoint_WhenModelBindingFails_PreservesHttpFailureStatus()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsync(
            "/rpc/CreateUser",
            new StringContent("""{"age":"invalid"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserDefinedEndpoint_WhenModelBindingFails_IsNotWrapped()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsync(
            "/custom",
            new StringContent("""{"age":"invalid"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RpcHttpEndpoint_WhenContractReturnsBareType_WritesRawBody()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/rpc/ReadToken");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "header-token");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ReadTokenResponse>();
        Assert.NotNull(payload);
        Assert.Equal("header-token", payload.Token);
    }

    [Fact]
    public async Task RpcHttpEndpoint_WhenContractReturnsResultOfT_WritesBusinessResultBody()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync(
            "/rpc/CreateUser",
            new CreateUserRequest { Age = 18 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Result<CreateUserResponse>>();
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal(18, result.Value.Age);
    }

    [Fact]
    public async Task RpcHttpEndpoint_WhenRpcResultCarriesRedirectMetadata_WritesRedirectResponse()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/rpc/Redirect?url=https%3A%2F%2Fexample.com%2Fcallback");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://example.com/callback", response.Headers.Location?.ToString());
        Assert.Equal(bool.TrueString, response.Headers.GetValues(NOFAbstractionConstants.Transport.Headers.RpcSuccess).Single());
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RpcHttpEndpoint_WhenRpcResultCarriesFailureBodyAndHeaders_WritesThemToHttpResponse()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/rpc/TokenFailure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(bool.TrueString, response.Headers.GetValues(NOFAbstractionConstants.Transport.Headers.RpcSuccess).Single());
        Assert.Equal(
            "Bearer error=\"invalid_token\", error_description=\"access token expired.\"",
            response.Headers.WwwAuthenticate.ToString());

        var payload = await response.Content.ReadFromJsonAsync<TokenErrorBody>();
        Assert.NotNull(payload);
        Assert.Equal("invalid_token", payload.Error);
        Assert.Equal("access token expired.", payload.ErrorDescription);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddRouting();
        builder.Services.AddSingleton<IContextAccessor, ContextAccessor>();
        builder.Services.AddSingleton(new RequestInboundPipelineTypes());
        builder.Services.AddSingleton<RequestInboundPipelineExecutor>();
        builder.Services.AddScoped<RpcServerInvocationResolver>();
        builder.Services.AddScoped<HttpRequestInboundAdapter>();
        builder.Services.AddOptions<HttpHeaderOutboundOptions>();

        builder.Services.AddSingleton<ValidationRpcServer>();
        builder.Services.AddTransient<CreateUserHandler>();
        builder.Services.AddTransient<ReadTokenHandler>();
        builder.Services.AddTransient<RedirectHandler>();
        builder.Services.AddTransient<TokenFailureHandler>();

        var registry = new Registry();
        registry.RpcServerRegistry.Add(new RpcServerRegistration(typeof(IValidationRpcService), typeof(ValidationRpcServer)));
        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton(registry.RpcServerRegistry);

        var app = builder.Build();
        app.MapHttpEndpoint<ValidationRpcServer>("/rpc");
        app.MapPost("/custom", ([FromBody] CreateUserRequest request) => Results.Ok(Result.Success(new CreateUserResponse(request.Age))));
        await app.StartAsync();
        return app;
    }

    public sealed class CreateUserRequest
    {
        public int Age { get; set; }
    }

    public sealed class ReadTokenRequest
    {
        [NOF.Contract.FromHeader(NOFAbstractionConstants.Transport.Headers.Authorization)]
        public HeaderToken Token { get; set; }
    }

    public sealed class RedirectRequest
    {
        public string Url { get; set; } = string.Empty;
    }

    public sealed record CreateUserResponse(int Age);

    public sealed record ReadTokenResponse(string Token);

    public sealed record TokenErrorBody
    {
        [JsonPropertyName("error")]
        public required string Error { get; init; }

        [JsonPropertyName("error_description")]
        public required string ErrorDescription { get; init; }
    }

    public readonly record struct HeaderToken(string Value) : ITransportStringParsable<HeaderToken>
    {
        public static bool TryParse(string? value, IFormatProvider? provider, out HeaderToken result)
        {
            var token = value ?? string.Empty;
            const string prefix = "Bearer ";
            if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                token = token[prefix.Length..].TrimStart();
            }

            result = new HeaderToken(token);
            return true;
        }
    }

    public partial interface IValidationRpcService : IRpcService
    {
        [HttpEndpoint(HttpVerb.Post, "/CreateUser")]
        Result<CreateUserResponse> CreateUser(CreateUserRequest request);

        [HttpEndpoint(HttpVerb.Get, "/ReadToken")]
        ReadTokenResponse ReadToken(ReadTokenRequest request);

        [HttpEndpoint(HttpVerb.Get, "/Redirect")]
        Empty Redirect(RedirectRequest request);

        [HttpEndpoint(HttpVerb.Get, "/TokenFailure")]
        ReadTokenResponse TokenFailure(Empty request);
    }

    public sealed class ValidationRpcServer : RpcServer<IValidationRpcService>, IRpcServer
    {
        public static IReadOnlyDictionary<string, RpcHandlerMapping> HandlerMappings { get; } =
            new Dictionary<string, RpcHandlerMapping>
            {
                [nameof(IValidationRpcService.CreateUser)] =
                    new(typeof(CreateUserHandler), typeof(CreateUserRequest), typeof(Result<CreateUserResponse>)),
                [nameof(IValidationRpcService.ReadToken)] =
                    new(typeof(ReadTokenHandler), typeof(ReadTokenRequest), typeof(ReadTokenResponse)),
                [nameof(IValidationRpcService.Redirect)] =
                    new(typeof(RedirectHandler), typeof(RedirectRequest), typeof(Empty)),
                [nameof(IValidationRpcService.TokenFailure)] =
                    new(typeof(TokenFailureHandler), typeof(Empty), typeof(ReadTokenResponse))
            };

        protected override IReadOnlyDictionary<string, RpcHandlerMapping> GetHandlerMappings() => HandlerMappings;
    }

    public sealed class CreateUserHandler : RpcHandler<CreateUserRequest, Result<CreateUserResponse>>
    {
        public override Task<RpcResult<Result<CreateUserResponse>>> HandleAsync(CreateUserRequest request, Context context, CancellationToken cancellationToken)
            => Task.FromResult(Success(Result.Success(new CreateUserResponse(request.Age))));
    }

    public sealed class ReadTokenHandler : RpcHandler<ReadTokenRequest, ReadTokenResponse>
    {
        public override Task<RpcResult<ReadTokenResponse>> HandleAsync(ReadTokenRequest request, Context context, CancellationToken cancellationToken)
            => Task.FromResult(Success(new ReadTokenResponse(request.Token.Value)));
    }

    public sealed class RedirectHandler : RpcHandler<RedirectRequest, Empty>
    {
        public override Task<RpcResult<Empty>> HandleAsync(RedirectRequest request, Context context, CancellationToken cancellationToken)
            => Task.FromResult(Success(HttpTransportMetadata.Create(302, [new KeyValuePair<string, string?>("Location", request.Url)])));
    }

    public sealed class TokenFailureHandler : RpcHandler<Empty, ReadTokenResponse>
    {
        public override Task<RpcResult<ReadTokenResponse>> HandleAsync(Empty request, Context context, CancellationToken cancellationToken)
            => Task.FromResult(Response(
                new TokenErrorBody
                {
                    Error = "invalid_token",
                    ErrorDescription = "access token expired."
                },
                HttpTransportMetadata.Create(
                    401,
                    [
                        new KeyValuePair<string, string?>(
                            "WWW-Authenticate",
                            "Bearer error=\"invalid_token\", error_description=\"access token expired.\"")
                    ])));
    }
}
