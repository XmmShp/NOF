using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using System.Diagnostics.CodeAnalysis;
namespace NOF.Hosting.AspNetCore;

internal sealed class RpcServerHttpEndpointInitializationStep(Type rpcServerType) : IApplicationInitializationStep
{
    public TopologyComparison Compare(IApplicationInitializationStep other)
        => other is DaemonServiceResolutionInitializationStep
            ? TopologyComparison.After
            : TopologyComparison.DoesNotMatter;

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Automatic RPC HTTP endpoint mapping is only used by ASP.NET Core hosts that opt into this transport.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Automatic RPC HTTP endpoint mapping is only used by ASP.NET Core hosts that opt into this transport.")]
    public Task ExecuteAsync(IHost app)
    {
        if (app is IEndpointRouteBuilder routeBuilder)
        {
            NOFHostingAspNetCoreExtensions.MapHttpEndpoint(routeBuilder, rpcServerType);
        }

        return Task.CompletedTask;
    }
}
