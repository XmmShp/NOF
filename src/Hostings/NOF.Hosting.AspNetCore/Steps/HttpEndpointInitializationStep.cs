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

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "IApplicationInitializationStep.ExecuteAsync cannot carry RequiresUnreferencedCode, but ASP.NET Core RPC endpoint auto-mapping intentionally calls the reflection-based mapper.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "IApplicationInitializationStep.ExecuteAsync cannot carry RequiresDynamicCode, but ASP.NET Core RPC endpoint auto-mapping intentionally calls the reflection-based mapper.")]
    public Task ExecuteAsync(IHost app)
    {
        if (app is IEndpointRouteBuilder routeBuilder)
        {
            NOFHostingAspNetCoreExtensions.MapHttpEndpoint(routeBuilder, rpcServerType);
        }

        return Task.CompletedTask;
    }
}
