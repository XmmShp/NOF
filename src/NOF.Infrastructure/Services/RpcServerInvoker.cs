using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

public static class RpcServerInvoker
{
    public static async Task<object?> InvokeAsync<TRpcService>(
        IServiceProvider rootServiceProvider,
        string operationName,
        object request,
        CancellationToken cancellationToken)
        where TRpcService : class, NOF.Contract.IRpcService
    {
        ArgumentNullException.ThrowIfNull(rootServiceProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(request);

        if (!RpcServerRegistry.TryGetImplementationType(typeof(TRpcService), out var serverType))
        {
            throw new InvalidOperationException($"RPC server is not registered for '{typeof(TRpcService).FullName}'.");
        }

        var server = rootServiceProvider.GetRequiredService(serverType) as IRpcServer
            ?? throw new InvalidOperationException($"Resolved RPC server '{serverType.FullName}' does not implement IRpcServer.");

        if (!server.TryGetHandlerType(operationName, out var handlerType))
        {
            throw new InvalidOperationException($"RPC handler mapping is missing for '{typeof(TRpcService).FullName}.{operationName}'.");
        }

        var methodInfo = typeof(TRpcService).GetMethod(operationName)
            ?? throw new InvalidOperationException($"RPC contract method '{typeof(TRpcService).FullName}.{operationName}' was not found.");

        var outboundPipeline = rootServiceProvider.GetRequiredService<IRequestOutboundPipelineExecutor>();
        var outboundContext = new RequestOutboundContext
        {
            Message = request,
            Services = rootServiceProvider,
            ServiceType = typeof(TRpcService),
            OperationName = operationName
        };

        await outboundPipeline.ExecuteAsync(outboundContext, async ct =>
        {
            await InboundHandlerInvoker.ExecuteRpcAsync(
                rootServiceProvider,
                request,
                methodInfo,
                handlerType,
                outboundContext.Headers,
                async (sp, innerCt) =>
                {
                    var handler = (RpcHandler)sp.GetRequiredService(handlerType);
                    outboundContext.Response = await handler.HandleAsync(request, innerCt).ConfigureAwait(false);
                },
                ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return outboundContext.Response;
    }
}
