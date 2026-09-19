using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using NOF.Test;
using System.Security.Claims;
using Xunit;

namespace NOF.Infrastructure.Tests.Middlewares;

public sealed class HostTenantInboundMiddlewareTests
{
    [Theory]
    [InlineData(nameof(IHostTenantTestService.Host), "host")]
    [InlineData(nameof(IHostTenantTestService.MetadataHost), "host")]
    [InlineData(nameof(IHostTenantTestService.Regular), "trusted")]
    [InlineData(nameof(IHostTenantTestService.Disabled), "trusted")]
    public async Task DefaultPipeline_ShouldSelectTenantBeforeResolvingHandlerDependencies(string method, string expectedTenant)
    {
        using var ambient = Context.PushCurrent(Context.Empty.WithTenantId("caller"));
        using var provider = CreateProvider(authenticated: true);
        using var scope = provider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<RequestInboundPipelineExecutor>();
        var result = await pipeline.ExecuteAsync(new Empty(), typeof(TenantProbeHandler), typeof(Result),
            typeof(IHostTenantTestService), method,
            [new(NOFAbstractionConstants.Transport.Headers.TenantId, "untrusted")], default);

        Assert.True(result.Response!.IsSuccess);
        var probe = provider.GetRequiredService<TenantProbe>();
        Assert.Equal(expectedTenant, probe.DependencyTenant);
        Assert.Equal(expectedTenant, probe.HandlerTenant);
        Assert.Equal(expectedTenant, probe.AmbientTenant);
        Assert.Equal("caller", Context.Current.TenantId);
    }

    [Fact]
    public async Task HostTenant_ShouldNotBypassAuthorization()
    {
        using var provider = CreateProvider(authenticated: false);
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<RequestInboundPipelineExecutor>()
            .ExecuteAsync(new Empty(), typeof(TenantProbeHandler), typeof(Result), typeof(IHostTenantTestService),
                nameof(IHostTenantTestService.Host), null, default);

        Assert.Equal("401", result.Response!.ErrorCode);
        Assert.Null(provider.GetRequiredService<TenantProbe>().DependencyTenant);
    }

    [Fact]
    public async Task LocalRpc_ShouldUseHostTenantAndRestoreCallerOnFailure()
    {
        using var ambient = Context.PushCurrent(Context.Empty.WithTenantId("caller"));
        using var provider = CreateProvider(authenticated: true);
        using var scope = provider.CreateScope();
        var result = await RpcServerInvoker.InvokeAsync<IHostTenantTestService>(scope.ServiceProvider,
            typeof(IHostTenantTestService).GetMethod(nameof(IHostTenantTestService.Host))!, new Empty(),
            Context.Empty.WithTenantId("explicit"), default);
        Assert.True(result!.IsSuccess);
        Assert.Equal(NOFAbstractionConstants.Tenant.HostId, provider.GetRequiredService<TenantProbe>().DependencyTenant);
        Assert.Equal("caller", Context.Current.TenantId);

        provider.GetRequiredService<TenantProbe>().Throw = true;
        var failed = await RpcServerInvoker.InvokeAsync<IHostTenantTestService>(
            scope.ServiceProvider, typeof(IHostTenantTestService).GetMethod(nameof(IHostTenantTestService.Host))!,
            new Empty(), Context.Empty, default);
        Assert.False(failed!.IsSuccess);
        Assert.Equal("caller", Context.Current.TenantId);
    }

    [Theory]
    [InlineData(nameof(IHostTenantTestService.Host), "host")]
    [InlineData(nameof(IHostTenantTestService.MetadataHost), "host")]
    [InlineData(nameof(IHostTenantTestService.Regular), "existing")]
    [InlineData(nameof(IHostTenantTestService.Disabled), "existing")]
    public async Task Outbound_ShouldOverrideTenantHeaderOnlyForHostMethods(string method, string expectedTenant)
    {
        var context = new RequestOutboundContext(Context.Empty.WithTenantId("explicit"))
        {
            ServiceType = typeof(IHostTenantTestService),
            MethodInfo = typeof(IHostTenantTestService).GetMethod(method)!
        };
        context.Headers[NOFAbstractionConstants.Transport.Headers.TenantId] = "existing";
        await new TenantHeaderOutboundMiddleware().InvokeAsync(context, new Empty(),
            static (_, _, _) => ValueTask.CompletedTask, default);
        Assert.Equal(expectedTenant, context.Headers[NOFAbstractionConstants.Transport.Headers.TenantId]);
    }

    private static ServiceProvider CreateProvider(bool authenticated)
    {
        var builder = NOFTestAppBuilder.Create();
        var user = new UserContext();
        if (authenticated)
        {
            user.User.AddIdentity(new ClaimsIdentity([
                new Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "user"),
                new Claim(ClaimTypes.TenantId, "trusted")], "test"));
        }

        builder.Services.AddSingleton<IUserContext>(user);
        builder.Services.AddSingleton<TenantProbe>();
        builder.Services.AddScoped<TenantDependency>();
        builder.Services.AddScoped<TenantProbeHandler>();
        builder.Services.AddScoped<IRequestOutboundPipelineExecutor, RequestOutboundPipelineExecutor>();
        builder.Services.AddScoped<HostTenantTestServer>();
        var registry = new RpcServerRegistry();
        registry.Add(new RpcServerRegistration(typeof(IHostTenantTestService), typeof(HostTenantTestServer)));
        builder.Services.AddSingleton(registry);
        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}

[TransportOverMemory]
public interface IHostTenantTestService : IRpcService
{
    [UseHostTenant]
    [RequirePermission]
    Result Host(Empty request);

    [Metadata("NOF.TENANT.USE_HOST", "true")]
    Result MetadataHost(Empty request);

    Result Regular(Empty request);

    [Metadata(UseHostTenantAttribute.MetadataKey, "false")]
    Result Disabled(Empty request);
}

public interface IHostTenantTestServiceClient : IRpcClient<IHostTenantTestService>;

public sealed class TenantProbe
{
    public string? DependencyTenant { get; set; }
    public string? HandlerTenant { get; set; }
    public string? AmbientTenant { get; set; }
    public bool Throw { get; set; }
}

public sealed class TenantDependency
{
    public string TenantId { get; } = Context.Current.TenantId;
}

public sealed class TenantProbeHandler : RpcHandler<Empty, Result>
{
    private readonly TenantProbe _probe;

    public TenantProbeHandler(TenantDependency dependency, TenantProbe probe)
    {
        _probe = probe;
        probe.DependencyTenant = dependency.TenantId;
    }

    public override Task<Result> HandleAsync(Empty request, Context context, CancellationToken cancellationToken)
    {
        _probe.HandlerTenant = context.TenantId;
        _probe.AmbientTenant = Context.Current.TenantId;
        if (_probe.Throw)
        {
            throw new InvalidOperationException("Simulated handler failure.");
        }

        return Task.FromResult(Result.Success());
    }
}

public sealed class HostTenantTestServer : RpcServer<IHostTenantTestService>
{
    private static readonly IReadOnlyDictionary<string, RpcHandlerMapping> _mappings =
        new Dictionary<string, RpcHandlerMapping>
        {
            [nameof(IHostTenantTestService.Host)] = new(typeof(TenantProbeHandler), typeof(Empty), typeof(Result))
        };

    protected override IReadOnlyDictionary<string, RpcHandlerMapping> GetHandlerMappings() => _mappings;
}
