using Microsoft.Extensions.DependencyInjection;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Infrastructure;

public static class RpcServerInvoker
{
    public static async Task<IResult?> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TRpcService>(
        IServiceProvider rootServiceProvider,
        MethodInfo serviceMethodInfo,
        object request,
        Context context,
        CancellationToken cancellationToken)
        where TRpcService : class, IRpcService
    {
        ArgumentNullException.ThrowIfNull(rootServiceProvider);
        ArgumentNullException.ThrowIfNull(serviceMethodInfo);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var operationName = serviceMethodInfo.Name;
        var invocationResolver = rootServiceProvider.GetRequiredService<RpcServerInvocationResolver>();
        var resolution = invocationResolver.Resolve<TRpcService>(operationName);

        var outboundPipeline = rootServiceProvider.GetRequiredService<RequestOutboundPipelineExecutor>();
        var outboundContext = new RequestOutboundContext(context)
        {
            ServiceType = typeof(TRpcService),
            MethodInfo = serviceMethodInfo
        };

        await outboundPipeline.ExecuteAsync(outboundContext, request, async (_, currentRequest, ct) =>
        {
            var inboundPipeline = rootServiceProvider.GetRequiredService<RequestInboundPipelineExecutor>();
            var inboundContext = await inboundPipeline.ExecuteAsync(
                currentRequest,
                resolution.HandlerMapping.HandlerType,
                resolution.HandlerMapping.ReturnType,
                typeof(TRpcService),
                operationName,
                outboundContext.Headers,
                ct).ConfigureAwait(false);
            outboundContext.Response = inboundContext.Response;
        }, cancellationToken).ConfigureAwait(false);

        return outboundContext.Response;
    }
}
