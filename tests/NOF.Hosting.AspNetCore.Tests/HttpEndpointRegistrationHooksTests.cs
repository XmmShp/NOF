using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace NOF.Hosting.AspNetCore.Tests;

public sealed class HttpEndpointRegistrationTests
{
    [Fact]
    public async Task AddRpcServer_ShouldMapRpcEndpointAutomatically()
    {
        var builder = NOFWebApplicationBuilder.Create([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.AddRpcServer<HookedRpcServer>();
        builder.Services.AddTransient<EchoHandler>();

        await using var app = await builder.BuildAsync();
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/Echo", new EchoRequest
        {
            Value = "hello"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<Result<EchoResponse>>();
        Assert.NotNull(payload);
        Assert.True(payload.IsSuccess);
        Assert.Equal("hello", payload.Value!.Value);
    }

    [Fact]
    public async Task AddRpcServer_ShouldRegisterRpcServerOnlyOnce()
    {
        var builder = NOFWebApplicationBuilder.Create([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.AddRpcServer<HookedRpcServer>();
        builder.AddRpcServer<HookedRpcServer>();
        builder.Services.AddTransient<EchoHandler>();

        await using var app = await builder.BuildAsync();
        await app.StartAsync();

        var routeEndpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(static endpoint => string.Equals(endpoint.RoutePattern.RawText, "/Echo", StringComparison.Ordinal))
            .ToArray();

        Assert.Single(routeEndpoints);
    }

    [Fact]
    public async Task AddRpcServer_WithMemoryTransport_ShouldNotMapHttpEndpoint()
    {
        var builder = NOFWebApplicationBuilder.Create([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.AddRpcServer<MemoryRpcServer>();
        builder.Services.AddTransient<MemoryEchoHandler>();

        await using var app = await builder.BuildAsync();
        await app.StartAsync();

        var routeEndpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(static endpoint => string.Equals(endpoint.RoutePattern.RawText, "/MemoryEcho", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(routeEndpoints);

        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/MemoryEcho", new EchoRequest
        {
            Value = "local only"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddRpcServer_ShouldUseControllerRpcContractRoutePrefix()
    {
        var builder = NOFWebApplicationBuilder.Create([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.AddRpcServer<PrefixedControllerRpcServer>();
        builder.Services.AddTransient<PrefixedEchoHandler>();

        await using var app = await builder.BuildAsync();
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/controller-rpc/Echo", new EchoRequest
        {
            Value = "prefixed"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<Result<EchoResponse>>();
        Assert.NotNull(payload);
        Assert.True(payload.IsSuccess);
        Assert.Equal("prefixed", payload.Value!.Value);
    }

    public sealed class EchoRequest
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed record EchoResponse(string Value);

    public partial interface IHookedRpcService : IRpcService
    {
        [HttpEndpoint(HttpVerb.Post, "/Echo")]
        Result<EchoResponse> Echo(EchoRequest request);
    }

    public sealed class HookedRpcServer : RpcServer<IHookedRpcService>, IRpcServer
    {
        public static IReadOnlyDictionary<string, RpcHandlerMapping> HandlerMappings { get; } =
            new Dictionary<string, RpcHandlerMapping>
            {
                [nameof(IHookedRpcService.Echo)] =
                    new(typeof(EchoHandler), typeof(EchoRequest), typeof(Result<EchoResponse>))
            };

        protected override IReadOnlyDictionary<string, RpcHandlerMapping> GetHandlerMappings() => HandlerMappings;
    }

    public sealed class EchoHandler : RpcHandler<EchoRequest, Result<EchoResponse>>
    {
        public override Task<Result<EchoResponse>> HandleAsync(EchoRequest request, Context context, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success(new EchoResponse(request.Value)));
    }

    [TransportOverHttp(HttpRpcStyle.ControllerRpc, "controller-rpc/")]
    public partial interface IPrefixedControllerRpcService : IRpcService
    {
        [HttpEndpoint(HttpVerb.Post, "/Echo")]
        Result<EchoResponse> Echo(EchoRequest request);
    }

    public sealed class PrefixedControllerRpcServer : RpcServer<IPrefixedControllerRpcService>, IRpcServer
    {
        public static IReadOnlyDictionary<string, RpcHandlerMapping> HandlerMappings { get; } =
            new Dictionary<string, RpcHandlerMapping>
            {
                [nameof(IPrefixedControllerRpcService.Echo)] =
                    new(typeof(PrefixedEchoHandler), typeof(EchoRequest), typeof(Result<EchoResponse>))
            };

        protected override IReadOnlyDictionary<string, RpcHandlerMapping> GetHandlerMappings() => HandlerMappings;
    }

    public sealed class PrefixedEchoHandler : RpcHandler<EchoRequest, Result<EchoResponse>>
    {
        public override Task<Result<EchoResponse>> HandleAsync(EchoRequest request, Context context, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success(new EchoResponse(request.Value)));
    }

    [TransportOverMemory]
    public interface IMemoryRpcService : IRpcService
    {
        Result<EchoResponse> MemoryEcho(EchoRequest request);
    }

    public sealed class MemoryRpcServer : RpcServer<IMemoryRpcService>, IRpcServer
    {
        public static IReadOnlyDictionary<string, RpcHandlerMapping> HandlerMappings { get; } =
            new Dictionary<string, RpcHandlerMapping>
            {
                [nameof(IMemoryRpcService.MemoryEcho)] =
                    new(typeof(MemoryEchoHandler), typeof(EchoRequest), typeof(Result<EchoResponse>))
            };

        protected override IReadOnlyDictionary<string, RpcHandlerMapping> GetHandlerMappings() => HandlerMappings;
    }

    public sealed class MemoryEchoHandler : RpcHandler<EchoRequest, Result<EchoResponse>>
    {
        public override Task<Result<EchoResponse>> HandleAsync(EchoRequest request, Context context, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success(new EchoResponse(request.Value)));
    }
}
