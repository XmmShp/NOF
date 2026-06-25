using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Hosting;
using NOF.Hosting.AspNetCore;
namespace NOF.Hosting.AspNetCore;

internal static class AspNetCoreRpcServerRegistration
{
    private static readonly object RpcServerHttpEndpointStepsKey = new();

    public static void Register(INOFAppBuilder builder, Type rpcServerType)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(rpcServerType);

        if (builder is not NOFWebApplicationBuilder)
        {
            return;
        }

        var registeredRpcServers = GetOrAddRegisteredRpcServers(builder);
        if (!registeredRpcServers.Add(rpcServerType))
        {
            return;
        }

        builder.Services.TryAddSingleton<HttpEndpointMappingState>();
        builder.Services.AddInitializationStep(new RpcServerHttpEndpointInitializationStep(rpcServerType));
    }

    private static HashSet<Type> GetOrAddRegisteredRpcServers(INOFAppBuilder builder)
    {
        if (builder.Properties.TryGetValue(RpcServerHttpEndpointStepsKey, out var existing)
            && existing is HashSet<Type> registeredRpcServers)
        {
            return registeredRpcServers;
        }

        registeredRpcServers = [];
        builder.Properties[RpcServerHttpEndpointStepsKey] = registeredRpcServers;
        return registeredRpcServers;
    }
}
