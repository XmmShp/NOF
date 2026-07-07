using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Security.Claims;
using Xunit;

namespace NOF.Infrastructure.Tests;

public sealed class LocalRpcClientAuthorizationIntegrationTests
{
    [Fact]
    public async Task LocalRpcClient_ShouldReturn401_WhenUserIsUnauthenticated()
    {
        using var provider = BuildServiceProvider();

        var client = provider.GetRequiredService<ProtectedFleetClient>();
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
        using var provider = BuildServiceProvider();
        var userContext = (UserContext)provider.GetRequiredService<IUserContext>();
        userContext.Logout();
        userContext.User.AddIdentity(TestPrincipalFactory.CreateAuthenticatedIdentity((ClaimTypes.Permission, "fleet.read")));

        var client = provider.GetRequiredService<ProtectedFleetClient>();
        Result<GetFleetOverviewResponse> result = await client.GetFleetOverviewAsync(new Empty(), Context.Empty);
        var recorder = provider.GetRequiredService<InvocationRecorder>();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("fleet", result.Value.Name);
        Assert.Equal(1, recorder.Count);
    }

    [Fact]
    public async Task LocalRpcClient_ShouldInvokeHandlerAndInMemoryEventHandlerInClientScope()
    {
        using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();

        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var marker = scope.ServiceProvider.GetRequiredService<LocalScopeMarker>();
        var client = scope.ServiceProvider.GetRequiredService<ProtectedFleetClient>();

        var result = await client.CheckScopeAsync(new ScopeCheckRequest(dbContext, marker), Context.Empty);
        var probe = scope.ServiceProvider.GetRequiredService<LocalScopeProbe>();

        Assert.True(result.IsSuccess);
        Assert.True(probe.HandlerUsedClientScopeDbContext);
        Assert.True(probe.EventHandlerUsedHandlerDbContext);
        Assert.True(probe.EventHandlerUsedClientScopeMarker);
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
        services.AddSingleton<ProtectedFleetServer>();
        services.AddTransient<GetFleetOverviewHandler>();
        services.AddTransient<ScopeCheckHandler>();
        services.AddTransient<ScopeCheckEventHandler>();
        services.AddTransient<ProtectedFleetClient>();
        services.AddScoped<IInboundAuthorizationHandler, DefaultInboundAuthorizationHandler>();
        services.AddTransient<AuthorizationInboundMiddleware>();
        services.AddRequestInboundMiddleware<AuthorizationInboundMiddleware>();
        services.AddScoped<IEventPublisher, InMemoryEventPublisher>();
        services.AddScoped<IDaemonService, EventPublisherAmbientDaemonService>();

        services.AddScoped<RequestInboundPipelineExecutor>();
        services.AddSingleton<RequestOutboundPipelineExecutor>();
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

        return services.BuildServiceProvider();
    }
}

[LocalRpcClient<IProtectedFleetServiceClient>]
public partial class ProtectedFleetClient;

public partial interface IProtectedFleetService : IRpcService
{
    [RequirePermission("fleet.read")]
    Result<GetFleetOverviewResponse> GetFleetOverview(Empty request);

    Result CheckScope(ScopeCheckRequest request);
}

public partial interface IProtectedFleetServiceClient : IRpcClient
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
    public bool HandlerUsedClientScopeDbContext { get; set; }

    public bool EventHandlerUsedHandlerDbContext { get; set; }

    public bool EventHandlerUsedClientScopeMarker { get; set; }
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

public sealed class ScopeCheckHandler(
    IDbContext dbContext,
    LocalScopeMarker marker,
    LocalScopeProbe probe) : RpcHandler<ScopeCheckRequest, Result>
{
    public override Task<Result> HandleAsync(ScopeCheckRequest request, Context context, CancellationToken cancellationToken)
    {
        probe.HandlerUsedClientScopeDbContext = ReferenceEquals(request.ExpectedDbContext, dbContext);
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
        probe.EventHandlerUsedClientScopeMarker = ReferenceEquals(@event.HandlerMarker, marker);
        return Task.CompletedTask;
    }
}

public sealed class LocalScopedDbContext : IDbContext
{
    public IDbSet<TEntity> Set<TEntity>()
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
