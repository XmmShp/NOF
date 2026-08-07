using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using NOF.Hosting;
using System.Security.Claims;
using Xunit;

namespace NOF.Infrastructure.Tests;

public sealed class LocalRpcClientAuthorizationIntegrationTests
{
    [Fact]
    public async Task LocalRpcClient_ShouldReturn401_WhenUserIsUnauthenticated()
    {
        await using var provider = BuildServiceProvider();
        await using var callerScope = provider.CreateAsyncScope();

        var client = callerScope.ServiceProvider.GetRequiredService<LocalProtectedFleetServerClient>();
        Result<GetFleetOverviewResponse> result = await client.GetFleetOverviewAsync(new Empty(), Context.Empty);
        var recorder = provider.GetRequiredService<InvocationRecorder>();

        Assert.False(result.IsSuccess);
        Assert.Equal("401", result.ErrorCode);
        Assert.Equal("Please login first", result.Message);
        Assert.Equal(0, recorder.Count);
    }

    [Fact]
    public async Task LocalRpcClient_ShouldInvokeHandler_WhenUserHasPermission()
    {
        await using var provider = BuildServiceProvider();
        await using var callerScope = provider.CreateAsyncScope();
        var userContext = (UserContext)callerScope.ServiceProvider.GetRequiredService<IUserContext>();
        userContext.Logout();
        userContext.User.AddIdentity(TestPrincipalFactory.CreateAuthenticatedIdentity((ClaimTypes.Permission, "fleet.read")));

        var client = callerScope.ServiceProvider.GetRequiredService<LocalProtectedFleetServerClient>();
        Result<GetFleetOverviewResponse> result = await client.GetFleetOverviewAsync(new Empty(), Context.Empty);
        var recorder = provider.GetRequiredService<InvocationRecorder>();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("fleet", result.Value.Name);
        Assert.Equal(1, recorder.Count);
    }

    [Fact]
    public async Task LocalRpcClient_ShouldUseCallerScopeForOutboundAndNewScopeForEachInboundInvocation()
    {
        await using var provider = BuildServiceProvider();
        await using var callerScope = provider.CreateAsyncScope();

        var dbContext = callerScope.ServiceProvider.GetRequiredService<IDbContext>();
        var marker = callerScope.ServiceProvider.GetRequiredService<LocalScopeMarker>();
        var client = callerScope.ServiceProvider.GetRequiredService<LocalProtectedFleetServerClient>();

        var firstResult = await client.CheckScopeAsync(new ScopeCheckRequest(dbContext, marker), Context.Empty);
        var secondResult = await client.CheckScopeAsync(new ScopeCheckRequest(dbContext, marker), Context.Empty);
        var probe = callerScope.ServiceProvider.GetRequiredService<LocalScopeProbe>();

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.True(probe.OutboundUsedCallerScopeMarker);
        Assert.False(probe.HandlerUsedCallerScopeDbContext);
        Assert.False(probe.HandlerUsedCallerScopeMarker);
        Assert.True(probe.EventHandlerUsedHandlerDbContext);
        Assert.True(probe.EventHandlerUsedHandlerScopeMarker);
        Assert.Equal(2, probe.HandlerScopeMarkers.Count);
        Assert.All(probe.HandlerScopeMarkers, handlerMarker => Assert.NotSame(marker, handlerMarker));
        Assert.NotSame(probe.HandlerScopeMarkers[0], probe.HandlerScopeMarkers[1]);
        Assert.Equal(2, probe.DaemonServiceActivations);
        Assert.Equal(2, probe.DaemonServiceAsyncDisposals);
        Assert.Equal(2, probe.OutboundObservedDisposedInboundScopes);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton<IUserContext, UserContext>();
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(static sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<IMutableCurrentTenant>(static sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<IDbContext, LocalScopedDbContext>();
        services.AddScoped<LocalScopeMarker>();
        services.AddSingleton<InvocationRecorder>();
        services.AddSingleton<LocalScopeProbe>();
        services.AddScoped<ProtectedFleetServer>();
        services.AddTransient<GetFleetOverviewHandler>();
        services.AddTransient<ScopeCheckHandler>();
        services.AddTransient<ScopeCheckEventHandler>();
        services.AddTransient<LocalProtectedFleetServerClient>();
        services.AddRequestOutboundMiddleware<ScopeCheckOutboundMiddleware>();
        services.AddScoped<IInboundAuthorizationHandler, DefaultInboundAuthorizationHandler>();
        services.AddTransient<AuthorizationInboundMiddleware>();
        services.AddRequestInboundMiddleware<AuthorizationInboundMiddleware>();
        services.AddScoped<IEventPublisher, InMemoryEventPublisher>();
        services.AddScoped<IDaemonService, EventPublisherAmbientDaemonService>();
        services.AddScoped<IDaemonService, ScopeLifecycleDaemonService>();

        services.AddScoped<RequestInboundPipelineExecutor>();
        services.AddScoped<IRequestOutboundPipelineExecutor, RequestOutboundPipelineExecutor>();
        services.AddScoped<RpcServerInvocationResolver>();

        var rpcServerRegistry = new RpcServerRegistry();
        rpcServerRegistry.Add(new RpcServerRegistration(typeof(IProtectedFleetService), typeof(ProtectedFleetServer)));
        services.AddSingleton(rpcServerRegistry);
        services.AddSingleton(_ =>
        {
            var registry = new EventHandlerRegistry();
            registry.Add(new EventHandlerRegistration(typeof(ScopeCheckEventHandler), typeof(ScopeCheckEvent)));
            return registry;
        });

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}

[TransportOverMemory]
public partial interface IProtectedFleetService : IRpcService
{
    [RequirePermission("fleet.read")]
    Result<GetFleetOverviewResponse> GetFleetOverview(Empty request);

    Result CheckScope(ScopeCheckRequest request);
}

public partial interface IProtectedFleetServiceClient : IRpcClient<IProtectedFleetService>
{
    Task<Result<GetFleetOverviewResponse>> GetFleetOverviewAsync(
        Empty request,
        Context context,
        CancellationToken cancellationToken = default);

    Task<Result> CheckScopeAsync(
        ScopeCheckRequest request,
        Context context,
        CancellationToken cancellationToken = default);
}

public sealed record GetFleetOverviewResponse(string Name);

public sealed record ScopeCheckRequest(IDbContext ExpectedDbContext, LocalScopeMarker ExpectedMarker);

public sealed record ScopeCheckEvent(IDbContext HandlerDbContext, LocalScopeMarker HandlerMarker);

public sealed class LocalScopeMarker;

public sealed class LocalScopeProbe
{
    public bool OutboundUsedCallerScopeMarker { get; set; }

    public bool HandlerUsedCallerScopeDbContext { get; set; }

    public bool HandlerUsedCallerScopeMarker { get; set; }

    public bool EventHandlerUsedHandlerDbContext { get; set; }

    public bool EventHandlerUsedHandlerScopeMarker { get; set; }

    public List<LocalScopeMarker> HandlerScopeMarkers { get; } = [];

    public int DaemonServiceActivations { get; set; }

    public int DaemonServiceAsyncDisposals { get; set; }

    public int OutboundObservedDisposedInboundScopes { get; set; }
}

public sealed class InvocationRecorder
{
    public int Count { get; set; }
}

public sealed class ProtectedFleetServer : RpcServer<IProtectedFleetService>
{
    private static readonly IReadOnlyDictionary<string, RpcHandlerMapping> _mappings =
        new Dictionary<string, RpcHandlerMapping>
        {
            [nameof(IProtectedFleetService.GetFleetOverview)] =
                new(typeof(GetFleetOverviewHandler), typeof(Empty), typeof(Result<GetFleetOverviewResponse>)),
            [nameof(IProtectedFleetService.CheckScope)] =
                new(typeof(ScopeCheckHandler), typeof(ScopeCheckRequest), typeof(Result))
        };

    protected override IReadOnlyDictionary<string, RpcHandlerMapping> GetHandlerMappings() => _mappings;
}

public sealed class ScopeCheckOutboundMiddleware(
    LocalScopeMarker marker,
    LocalScopeProbe probe) : IRequestOutboundMiddleware
{
    public TopologyComparison Compare(IRequestOutboundMiddleware other)
        => TopologyComparison.DoesNotMatter;

    public async ValueTask InvokeAsync(
        RequestOutboundContext context,
        object request,
        RequestOutboundHandlerDelegate next,
        CancellationToken cancellationToken)
    {
        if (request is not ScopeCheckRequest scopeCheckRequest)
        {
            await next(context, request, cancellationToken).ConfigureAwait(false);
            return;
        }

        probe.OutboundUsedCallerScopeMarker = ReferenceEquals(scopeCheckRequest.ExpectedMarker, marker);
        var expectedDisposalCount = probe.DaemonServiceAsyncDisposals + 1;

        await next(context, request, cancellationToken).ConfigureAwait(false);

        if (probe.DaemonServiceAsyncDisposals >= expectedDisposalCount)
        {
            probe.OutboundObservedDisposedInboundScopes++;
        }
    }
}

public sealed class ScopeLifecycleDaemonService : IDaemonService, IAsyncDisposable
{
    private readonly LocalScopeProbe _probe;

    public ScopeLifecycleDaemonService(LocalScopeProbe probe)
    {
        _probe = probe;
        _probe.DaemonServiceActivations++;
    }

    public ValueTask DisposeAsync()
    {
        _probe.DaemonServiceAsyncDisposals++;
        return ValueTask.CompletedTask;
    }
}

public sealed class ScopeCheckHandler(
    IDbContext dbContext,
    LocalScopeMarker marker,
    LocalScopeProbe probe) : RpcHandler<ScopeCheckRequest, Result>
{
    public override Task<Result> HandleAsync(ScopeCheckRequest request, Context context, CancellationToken cancellationToken)
    {
        probe.HandlerUsedCallerScopeDbContext = ReferenceEquals(request.ExpectedDbContext, dbContext);
        probe.HandlerUsedCallerScopeMarker = ReferenceEquals(request.ExpectedMarker, marker);
        probe.HandlerScopeMarkers.Add(marker);
        new ScopeCheckEvent(dbContext, marker).PublishAsEvent();
        return Task.FromResult(Result.Success());
    }
}

public sealed class ScopeCheckEventHandler(
    IDbContext dbContext,
    LocalScopeMarker marker,
    LocalScopeProbe probe) : InMemoryEventHandler<ScopeCheckEvent>
{
    public override Task HandleAsync(ScopeCheckEvent @event, CancellationToken cancellationToken)
    {
        probe.EventHandlerUsedHandlerDbContext = ReferenceEquals(@event.HandlerDbContext, dbContext);
        probe.EventHandlerUsedHandlerScopeMarker = ReferenceEquals(@event.HandlerMarker, marker);
        return Task.CompletedTask;
    }
}

public sealed class LocalScopedDbContext : IDbContext
{
    public IRepository<TEntity> Set<TEntity>()
        where TEntity : class
        => throw new NotSupportedException();

    public int SaveChanges()
        => 0;

    public int SaveChanges(bool acceptAllChangesOnSuccess)
        => 0;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public IDbContextTransaction BeginTransaction()
        => throw new NotSupportedException();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

public sealed class GetFleetOverviewHandler(InvocationRecorder recorder) : RpcHandler<Empty, Result<GetFleetOverviewResponse>>
{
    public override Task<Result<GetFleetOverviewResponse>> HandleAsync(Empty request, Context context, CancellationToken cancellationToken)
    {
        recorder.Count++;
        return Task.FromResult(Result.Success(new GetFleetOverviewResponse("fleet")));
    }
}

internal static class TestPrincipalFactory
{
    public static ClaimsIdentity CreateAuthenticatedIdentity(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user-1"));

        foreach (var (type, value) in claims)
        {
            identity.AddClaim(new Claim(type, value));
        }

        return identity;
    }
}
