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
        RpcResult<Result<GetFleetOverviewResponse>> result = await client.GetFleetOverviewAsync(new Empty());
        var recorder = provider.GetRequiredService<InvocationRecorder>();

        var fail = Assert.IsType<RpcResult<Result<GetFleetOverviewResponse>>>(result);
        Assert.False(fail.IsSuccess);
        Assert.Equal(401, fail.StatusCode);
        Assert.Equal("Please login first", fail.Body);
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
        var result = await client.GetFleetOverviewAsync(new Empty());
        var recorder = provider.GetRequiredService<InvocationRecorder>();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.IsSuccess);
        Assert.NotNull(result.Value.Value);
        Assert.Equal("fleet", result.Value.Value.Name);
        Assert.Equal(1, recorder.Count);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton<IUserContext, UserContext>();
        services.AddScoped<NOFContext>();

        services.AddSingleton<InvocationRecorder>();
        services.AddSingleton<ProtectedFleetServer>();
        services.AddTransient<GetFleetOverviewHandler>();
        services.AddTransient<ProtectedFleetClient>();
        services.AddTransient<IRequestAuthorizationPolicy, MetadataRequestAuthorizationPolicy>();
        services.AddTransient<AuthorizationInboundMiddleware>();

        var requestInboundTypes = new RequestInboundPipelineTypes();
        requestInboundTypes.Add<AuthorizationInboundMiddleware>();
        services.AddSingleton(requestInboundTypes);
        services.AddSingleton(new RequestOutboundPipelineTypes());

        services.AddSingleton<RequestInboundPipelineExecutor>();
        services.AddSingleton<RequestOutboundPipelineExecutor>();
        services.AddScoped<RpcServerInvocationResolver>();

        var registry = new Registry();
        registry.RpcServerRegistry.Add(new RpcServerRegistration(typeof(IProtectedFleetService), typeof(ProtectedFleetServer)));
        services.AddSingleton(registry);
        services.AddSingleton(registry.RpcServerRegistry);

        return services.BuildServiceProvider();
    }
}

[LocalRpcClient<IProtectedFleetServiceClient>]
public partial class ProtectedFleetClient;

public partial interface IProtectedFleetService : IRpcService
{
    [RequirePermission("fleet.read")]
    Result<GetFleetOverviewResponse> GetFleetOverview(Empty request);
}

public partial interface IProtectedFleetServiceClient : IRpcClient
{
    Task<RpcResult<Result<GetFleetOverviewResponse>>> GetFleetOverviewAsync(Empty request, CancellationToken cancellationToken = default);
}

public sealed record GetFleetOverviewResponse(string Name);

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
                new(typeof(GetFleetOverviewHandler), typeof(Empty), typeof(Result<GetFleetOverviewResponse>))
        };

    protected override IReadOnlyDictionary<string, RpcHandlerMapping> GetHandlerMappings() => _mappings;
}

public sealed class GetFleetOverviewHandler(InvocationRecorder recorder) : RpcHandler<Empty, Result<GetFleetOverviewResponse>>
{
    public override Task<RpcResult<Result<GetFleetOverviewResponse>>> HandleAsync(Empty request, NOFContext context, CancellationToken cancellationToken)
    {
        recorder.Count++;
        return Task.FromResult(Success(Result.Success(new GetFleetOverviewResponse("fleet"))));
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
