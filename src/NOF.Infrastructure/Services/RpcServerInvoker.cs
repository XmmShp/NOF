using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Infrastructure;

public static class RpcServerInvoker
{
    public static async Task<IResult?> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TRpcService>(
        IServiceProvider callerServiceProvider,
        MethodInfo serviceMethodInfo,
        object request,
        Context context,
        CancellationToken cancellationToken)
        where TRpcService : class, IRpcService
    {
        ArgumentNullException.ThrowIfNull(callerServiceProvider);
        ArgumentNullException.ThrowIfNull(serviceMethodInfo);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var operationName = serviceMethodInfo.Name;

        var outboundPipeline = callerServiceProvider.GetRequiredService<IRequestOutboundPipelineExecutor>();
        var outboundContext = new RequestOutboundContext(context)
        {
            ServiceType = typeof(TRpcService),
            MethodInfo = serviceMethodInfo
        };

        await outboundPipeline.ExecuteAsync(outboundContext, request, async (currentContext, currentRequest, ct) =>
        {
            await using var inboundScope = callerServiceProvider.CreateAsyncScope();
            inboundScope.ServiceProvider.ResolveDaemonServices();

            var invocationResolver = inboundScope.ServiceProvider.GetRequiredService<RpcServerInvocationResolver>();
            var resolution = invocationResolver.Resolve<TRpcService>(operationName);
            var inboundPipeline = inboundScope.ServiceProvider.GetRequiredService<RequestInboundPipelineExecutor>();
            var inboundContext = await inboundPipeline.ExecuteAsync(
                currentRequest,
                resolution.HandlerMapping.HandlerType,
                resolution.HandlerMapping.ReturnType,
                typeof(TRpcService),
                operationName,
                currentContext.Headers,
                ct).ConfigureAwait(false);
            currentContext.Response = inboundContext.Response;
        }, cancellationToken).ConfigureAwait(false);

        return outboundContext.Response;
    }
}
