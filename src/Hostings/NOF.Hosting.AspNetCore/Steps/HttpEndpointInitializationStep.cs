using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Application;
using NOF.Contract;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
namespace NOF.Hosting.AspNetCore;

internal sealed class RpcServerHttpEndpointInitializationStep : IApplicationInitializationStep
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
            var registry = app.Services.GetRequiredService<RpcServerRegistry>();
            foreach (var registration in registry.Freeze())
            {
                var transport = registration.ServiceType.GetCustomAttribute<TransportOverAttribute>(inherit: false);
                if (transport is TransportOverHttpAttribute { Style: HttpRpcStyle.JsonRpc })
                {
                    NOFHostingAspNetCoreExtensions.MapJsonRpcEndpoint(routeBuilder, registration.ImplementationType);
                }
                else if (transport is null or TransportOverHttpAttribute)
                {
                    NOFHostingAspNetCoreExtensions.MapHttpEndpoint(routeBuilder, registration.ImplementationType);
                }
            }
        }

        return Task.CompletedTask;
    }
}
