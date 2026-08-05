using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Hosting.AspNetCore;

internal sealed class AspNetCoreRpcServerTransport : IRpcServerTransport
{
    public AspNetCoreRpcServerTransport()
    {
    }

    public TopologyComparison Compare(IRpcServerTransport other)
        => TopologyComparison.DoesNotMatter;

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ASP.NET Core RPC endpoint mapping intentionally calls the reflection-based mapper.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "ASP.NET Core RPC endpoint mapping intentionally calls the reflection-based mapper.")]
    public Task MapAsync(IHost host, RpcServerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(registration);

        if (host is not IEndpointRouteBuilder routeBuilder)
        {
            return Task.CompletedTask;
        }

        var transport = registration.ServiceType.GetCustomAttribute<TransportOverAttribute>(inherit: false);
        if (transport is TransportOverHttpAttribute { Style: HttpRpcStyle.JsonRpc })
        {
            NOFHostingAspNetCoreExtensions.MapJsonRpcEndpoint(routeBuilder, registration.ImplementationType);
        }
        else if (transport is null or TransportOverHttpAttribute)
        {
            NOFHostingAspNetCoreExtensions.MapHttpEndpoint(routeBuilder, registration.ImplementationType);
        }

        return Task.CompletedTask;
    }
}
