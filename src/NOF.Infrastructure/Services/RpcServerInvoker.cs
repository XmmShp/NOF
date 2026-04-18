using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;

namespace NOF.Infrastructure;

public static class RpcServerInvoker
{
    public static async Task<object?> InvokeAsync<TRpcService>(
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

        var outboundPipeline = rootServiceProvider.GetRequiredService<IRequestOutboundPipelineExecutor>();
        var outboundContext = new RequestOutboundContext
        {
            Message = request,
            Services = rootServiceProvider,
            ServiceType = typeof(TRpcService),
            MethodName = operationName
        };

        await outboundPipeline.ExecuteAsync(outboundContext, async ct =>
        {
            await using var scope = rootServiceProvider.CreateAsyncScope();
            ApplyHeaders(scope.ServiceProvider, outboundContext.Headers);

            var context = new RequestInboundContext
            {
                Message = request,
                Services = scope.ServiceProvider,
                HandlerType = handlerMapping.HandlerType,
                ServiceType = typeof(TRpcService),
                MethodName = operationName
            };

            var inboundPipeline = scope.ServiceProvider.GetRequiredService<IRequestInboundPipelineExecutor>();
            await inboundPipeline.ExecuteAsync(context, async innerCt =>
            {
                var handler = (RpcHandler)scope.ServiceProvider.GetRequiredService(handlerMapping.HandlerType);
                outboundContext.Response = await handler.HandleAsync(request, innerCt).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return outboundContext.Response;
    }

    private static void ApplyHeaders(IServiceProvider services, IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        if (headers is null)
        {
            return;
        }

        var executionContext = services.GetRequiredService<IExecutionContext>();
        foreach (var (headerKey, value) in headers)
        {
            executionContext[headerKey] = value;
        }
    }
}
