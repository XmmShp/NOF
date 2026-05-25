using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

public sealed record RpcServerInvocationResolution(
    Type ServerType,
    RpcHandlerMapping HandlerMapping);

public sealed class RpcServerInvocationResolver(
    IServiceProvider serviceProvider,
    RpcServerRegistry rpcServerRegistry)
{
    public RpcServerInvocationResolution Resolve<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TRpcService>(
        string operationName)
        where TRpcService : class, IRpcService
        => Resolve(typeof(TRpcService), operationName);

    public RpcServerInvocationResolution Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type serviceType,
        string operationName)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        if (!rpcServerRegistry.TryGetImplementationType(serviceType, out var serverType))
        {
            throw new InvalidOperationException($"RPC server is not registered for '{serviceType.FullName}'.");
        }

        var server = serviceProvider.GetRequiredService(serverType) as RpcServer
            ?? throw new InvalidOperationException($"Resolved RPC server '{serverType.FullName}' does not inherit RpcServer.");

        if (!server.TryGetHandlerMapping(operationName, out var handlerMapping))
        {
            throw new InvalidOperationException($"RPC handler mapping is missing for '{serviceType.FullName}.{operationName}'.");
        }

        return new RpcServerInvocationResolution(serverType, handlerMapping);
    }
}
