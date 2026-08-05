using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace NOF.Hosting.AspNetCore.Tests;

public sealed class JsonRpcEndpointIntegrationTests
{
    [Fact]
    public async Task JsonRpcEndpoint_ShouldInvokeMappedRpcServerUsingOperationNameOnly()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/contract-rpc", new
        {
            jsonrpc = "2.0",
            method = nameof(IPrefixedJsonRpcService.Echo),
            @params = new PrefixedEchoRequest { Value = "hello rpc" },
            id = "request-1"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        Assert.Equal("request-1", document.RootElement.GetProperty("id").GetString());
        var result = document.RootElement.GetProperty("result").Deserialize<Result<PrefixedEchoResponse>>(
            JsonSerializerOptions.NOF);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal("hello rpc", result.Value!.Value);
    }

    [Fact]
    public async Task JsonRpcEndpoint_WhenMethodDoesNotExist_ShouldReturnMethodError()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/contract-rpc", new
        {
            jsonrpc = "2.0",
            method = "Missing",
            @params = new { },
            id = "request-2"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        Assert.Equal(-32601, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal("request-2", document.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task JsonRpcEndpoint_WhenBodyIsInvalid_ShouldReturnParseErrorEnvelope()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsync(
            "/contract-rpc",
            new StringContent("not-json", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        Assert.Equal("2.0", document.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal(-32700, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("id").ValueKind);
    }

    [Fact]
    public async Task AddRpcServer_ShouldRegisterJsonRpcServerOnlyOnce()
    {
        var builder = NOFWebApplicationBuilder.Create([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.AddRpcServer<PrefixedJsonRpcServer>();
        builder.AddRpcServer<PrefixedJsonRpcServer>();
        builder.Services.AddTransient<PrefixedEchoHandler>();

        await using var app = await builder.BuildAsync();
        await app.StartAsync();

        var jsonRpcEndpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(static endpoint => endpoint.RoutePattern.RawText == "/contract-rpc")
            .ToArray();
        var endpoint = Assert.Single(jsonRpcEndpoints);
        Assert.Equal("/contract-rpc", endpoint.RoutePattern.RawText);
    }

    [Fact]
    public async Task AddRpcServer_ShouldUseJsonRpcContractRoutePrefix()
    {
        var builder = NOFWebApplicationBuilder.Create([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.AddRpcServer<PrefixedJsonRpcServer>();
        builder.Services.AddTransient<PrefixedEchoHandler>();

        await using var app = await builder.BuildAsync();
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/contract-rpc", new
        {
            jsonrpc = "2.0",
            method = nameof(IPrefixedJsonRpcService.Echo),
            @params = new PrefixedEchoRequest { Value = "contract route" },
            id = "contract-prefix"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        Assert.Equal("contract-prefix", document.RootElement.GetProperty("id").GetString());
        var result = document.RootElement.GetProperty("result").Deserialize<Result<PrefixedEchoResponse>>(
            JsonSerializerOptions.NOF);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal("contract route", result.Value!.Value);
    }

    private static async Task<Microsoft.AspNetCore.Builder.WebApplication> CreateAppAsync()
    {
        var builder = NOFWebApplicationBuilder.Create([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.AddRpcServer<PrefixedJsonRpcServer>();
        builder.Services.AddTransient<PrefixedEchoHandler>();

        var app = await builder.BuildAsync();
        await app.StartAsync();
        return app;
    }

    public sealed class PrefixedEchoRequest
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed record PrefixedEchoResponse(string Value);

    [TransportOverHttp(HttpRpcStyle.JsonRpc, "/contract-rpc")]
    public partial interface IPrefixedJsonRpcService : IRpcService
    {
        Result<PrefixedEchoResponse> Echo(PrefixedEchoRequest request);
    }

    public sealed class PrefixedJsonRpcServer : RpcServer<IPrefixedJsonRpcService>, IRpcServer
    {
        public static IReadOnlyDictionary<string, RpcHandlerMapping> HandlerMappings { get; } =
            new Dictionary<string, RpcHandlerMapping>
            {
                [nameof(IPrefixedJsonRpcService.Echo)] =
                    new(typeof(PrefixedEchoHandler), typeof(PrefixedEchoRequest), typeof(Result<PrefixedEchoResponse>))
            };

        protected override IReadOnlyDictionary<string, RpcHandlerMapping> GetHandlerMappings() => HandlerMappings;
    }

    public sealed class PrefixedEchoHandler : RpcHandler<PrefixedEchoRequest, Result<PrefixedEchoResponse>>
    {
        public override Task<Result<PrefixedEchoResponse>> HandleAsync(
            PrefixedEchoRequest request,
            Context context,
            CancellationToken cancellationToken)
            => Task.FromResult(Result.Success(new PrefixedEchoResponse(request.Value)));
    }
}
