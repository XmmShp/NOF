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
        where TRpcService : class, Contract.IRpcService
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
            MethodName = operationName
        };

        await outboundPipeline.ExecuteAsync(outboundContext, async ct =>
        {
            await using var scope = rootServiceProvider.CreateAsyncScope();
            ApplyHeaders(scope.ServiceProvider, outboundContext.Headers);

            var serviceType = methodInfo.DeclaringType
                ?? throw new InvalidOperationException("RPC method must have a declaring type.");
            var context = new RequestInboundContext
            {
                Message = request,
                Services = scope.ServiceProvider,
                HandlerType = handlerType,
                ServiceType = serviceType,
                MethodName = methodInfo.Name
            };

            var inboundPipeline = scope.ServiceProvider.GetRequiredService<IRequestInboundPipelineExecutor>();
            await inboundPipeline.ExecuteAsync(context, async innerCt =>
            {
                var handler = (RpcHandler)scope.ServiceProvider.GetRequiredService(handlerType);
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
