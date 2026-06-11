using Microsoft.Extensions.DependencyInjection;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

public static class RpcServerInvoker
{
    public static async Task<IRpcResult?> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TRpcService>(
        IServiceProvider rootServiceProvider,
        string operationName,
        object request,
        Context context,
        CancellationToken cancellationToken)
        where TRpcService : class, IRpcService
    {
        ArgumentNullException.ThrowIfNull(rootServiceProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var invocationResolver = rootServiceProvider.GetRequiredService<RpcServerInvocationResolver>();
        var resolution = invocationResolver.Resolve<TRpcService>(operationName);

        var outboundPipeline = rootServiceProvider.GetRequiredService<RequestOutboundPipelineExecutor>();
        var outboundContext = new RequestOutboundContext
        {
            Message = request,
            Context = context,
            ServiceType = typeof(TRpcService),
            MethodName = operationName
        };

        await outboundPipeline.ExecuteAsync(outboundContext, async ct =>
        {
            var inboundPipeline = rootServiceProvider.GetRequiredService<RequestInboundPipelineExecutor>();
            outboundContext.Response = await inboundPipeline.ExecuteAsync(
                request,
                resolution.HandlerMapping.HandlerType,
                typeof(TRpcService),
                operationName,
                outboundContext.Headers,
                ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return outboundContext.Response;
    }
}
