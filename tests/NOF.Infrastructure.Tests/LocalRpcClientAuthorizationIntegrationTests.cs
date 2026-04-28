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
        var result = await client.GetFleetOverviewAsync(new Empty());
        var recorder = provider.GetRequiredService<InvocationRecorder>();

        var fail = Assert.IsType<Result<GetFleetOverviewResponse>>(result);
        Assert.False(fail.IsSuccess);
        Assert.Equal("401", fail.ErrorCode);
        Assert.Equal("Please login first", fail.Message);
        Assert.Equal(0, recorder.Count);
    }

    [Fact]
    public async Task LocalRpcClient_ShouldInvokeHandler_WhenUserHasPermission()
    {
        using var provider = BuildServiceProvider();
        var userContext = (UserContext)provider.GetRequiredService<IUserContext>();
        userContext.User = TestPrincipalFactory.CreateAuthenticatedUser((ClaimTypes.Permission, "fleet.read"));

        var client = provider.GetRequiredService<ProtectedFleetClient>();
        var result = await client.GetFleetOverviewAsync(new Empty());
        var recorder = provider.GetRequiredService<InvocationRecorder>();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("fleet", result.Value.Name);
        Assert.Equal(1, recorder.Count);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton<IUserContext, UserContext>();
        services.AddScoped<ITransparentInfos, TransparentInfos>();

        services.AddSingleton<InvocationRecorder>();
        services.AddSingleton<ProtectedFleetServer>();
        services.AddTransient<GetFleetOverviewHandler>();
        services.AddTransient<ProtectedFleetClient>();
        services.AddTransient<AuthorizationInboundMiddleware>();

        var requestInboundTypes = new RequestInboundPipelineTypes();
        requestInboundTypes.Add<AuthorizationInboundMiddleware>();
        services.AddSingleton(requestInboundTypes);
        services.AddSingleton(new RequestOutboundPipelineTypes());

        services.AddSingleton<RequestInboundPipelineExecutor>();
        services.AddSingleton<RequestOutboundPipelineExecutor>();

        var rpcServerInfos = new RpcServerInfos();
        rpcServerInfos.Add(new RpcServerRegistration(typeof(IProtectedFleetService), typeof(ProtectedFleetServer)));
        services.AddSingleton(rpcServerInfos);

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
    Task<Result<GetFleetOverviewResponse>> GetFleetOverviewAsync(Empty request, CancellationToken cancellationToken = default);
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
    public override Task<Result<GetFleetOverviewResponse>> HandleAsync(Empty request, CancellationToken cancellationToken)
    {
        recorder.Count++;
        return Task.FromResult<Result<GetFleetOverviewResponse>>(Result.Success(new GetFleetOverviewResponse("fleet")));
    }
}

internal static class TestPrincipalFactory
{
    public static System.Security.Claims.ClaimsPrincipal CreateAuthenticatedUser(params (string Type, string Value)[] claims)
    {
        var identity = new System.Security.Claims.ClaimsIdentity("Test");
        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "user-1"));

        foreach (var (type, value) in claims)
        {
            identity.AddClaim(new System.Security.Claims.Claim(type, value));
        }

        return new System.Security.Claims.ClaimsPrincipal(identity);
    }
}
