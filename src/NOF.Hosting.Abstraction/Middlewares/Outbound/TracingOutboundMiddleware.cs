using NOF.Contract;
using System.Diagnostics;

namespace NOF.Hosting;

public sealed class TracingOutboundMiddleware : IOutboundMiddleware
{
    private readonly IExecutionContext _executionContext;

    public TracingOutboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public async ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        using var activity = NOFHostingConstants.Outbound.Source.StartActivity(
            $"Outbound: {context.Message.GetType().FullName}",
            ActivityKind.Producer);

        var currentActivity = Activity.Current;
        _executionContext[NOFContractConstants.Transport.Headers.TraceId] = currentActivity?.TraceId.ToString();
        _executionContext[NOFContractConstants.Transport.Headers.SpanId] = currentActivity?.SpanId.ToString();

        try
        {
            await next(cancellationToken);

            _executionContext.TryGetValue(NOFContractConstants.Transport.Headers.MessageId, out var messageId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.MessageId, messageId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.MessageType, context.Message.GetType().Name);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.TenantId, _executionContext.TenantId);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

