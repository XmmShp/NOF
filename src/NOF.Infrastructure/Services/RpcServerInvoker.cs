using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

public static class RpcServerInvoker
{
    public static async Task<object?> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TRpcService>(
        IServiceProvider rootServiceProvider,
        string operationName,
        object request,
        CancellationToken cancellationToken)
        where TRpcService : class, IRpcService
    {
        ArgumentNullException.ThrowIfNull(rootServiceProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(request);

        var rpcServerInfos = rootServiceProvider.GetRequiredService<RpcServerInfos>();
        if (!rpcServerInfos.TryGetImplementationType(typeof(TRpcService), out var serverType))
        {
            throw new InvalidOperationException($"RPC server is not registered for '{typeof(TRpcService).FullName}'.");
        }

        var server = rootServiceProvider.GetRequiredService(serverType) as RpcServer
            ?? throw new InvalidOperationException($"Resolved RPC server '{serverType.FullName}' does not inherit RpcServer.");

        if (!server.TryGetHandlerMapping(operationName, out var handlerMapping))
        {
            throw new InvalidOperationException($"RPC handler mapping is missing for '{typeof(TRpcService).FullName}.{operationName}'.");
        }

        var outboundPipeline = rootServiceProvider.GetRequiredService<RequestOutboundPipelineExecutor>();
        var outboundContext = new RequestOutboundContext
        {
            Message = request,
            ServiceType = typeof(TRpcService),
            MethodName = operationName
        };

        await outboundPipeline.ExecuteAsync(outboundContext, async ct =>
        {
            var inboundPipeline = rootServiceProvider.GetRequiredService<RequestInboundPipelineExecutor>();
            outboundContext.Response = await inboundPipeline.ExecuteAsync(
                request,
                handlerMapping.HandlerType,
                typeof(TRpcService),
                operationName,
                outboundContext.Headers,
                ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return outboundContext.Response;
    }
}
