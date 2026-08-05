using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Hosting.AspNetCore;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Microsoft.AspNetCore.Routing;

internal static partial class NOFHostingAspNetCoreExtensions
{
    [RequiresUnreferencedCode("JSON-RPC endpoint mapping uses runtime request and response type metadata.")]
    [RequiresDynamicCode("JSON-RPC endpoint mapping uses runtime request and response type metadata.")]
    internal static IEndpointRouteBuilder MapJsonRpcEndpoint(
        IEndpointRouteBuilder app,
        Type rpcServerType)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(rpcServerType);

        if (!typeof(RpcServer).IsAssignableFrom(rpcServerType)
            || !typeof(IRpcServer).IsAssignableFrom(rpcServerType))
        {
            throw new InvalidOperationException($"Type '{rpcServerType.FullName}' is not a valid RPC server type.");
        }

        var serviceType = rpcServerType.GetProperty(
                nameof(IRpcServerServiceType.ServiceType),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            ?.GetValue(null) as Type
            ?? throw new InvalidOperationException($"RPC server type '{rpcServerType.FullName}' does not expose static property '{nameof(IRpcServerServiceType.ServiceType)}'.");

        var handlerMappings = rpcServerType.GetProperty(
                nameof(IRpcServer.HandlerMappings),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            ?.GetValue(null) as IReadOnlyDictionary<string, RpcHandlerMapping>
            ?? throw new InvalidOperationException($"RPC server type '{rpcServerType.FullName}' does not expose static property '{nameof(IRpcServer.HandlerMappings)}'.");

        return MapJsonRpcEndpointCore(app, rpcServerType, serviceType, handlerMappings);
    }

    private static IEndpointRouteBuilder MapJsonRpcEndpointCore(
        IEndpointRouteBuilder app,
        Type rpcServerType,
        Type serviceType,
        IReadOnlyDictionary<string, RpcHandlerMapping> handlerMappings)
    {
        var resolvedPattern = GetHttpRoutePrefix(serviceType) ?? "/rpc";
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedPattern);
        resolvedPattern = NormalizeRoute(resolvedPattern);

        var mappingState = app.ServiceProvider.GetService<HttpEndpointMappingState>();
        var mappingKey = $"{rpcServerType.AssemblyQualifiedName}|JsonRpc";
        if (mappingState is not null && !mappingState.TryMarkMapped(mappingKey))
        {
            return app;
        }

        var handler = JsonRpcEndpointHandler.Create(serviceType, handlerMappings);
        app.MapPost(resolvedPattern, handler);
        return app;
    }
}
