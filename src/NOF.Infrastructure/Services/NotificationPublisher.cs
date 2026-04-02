using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure;

/// <summary>
/// Notification publisher implementation.
/// Runs the outbound pipeline (which handles tracing, headers, etc.) then dispatches via the rider.
/// </summary>
public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationRider _rider;
    private readonly IOutboundPipelineExecutor _outboundPipeline;
    private readonly IExecutionContext _executionContext;

    public NotificationPublisher(INotificationRider rider, IOutboundPipelineExecutor outboundPipeline, IExecutionContext executionContext)
    {
        _rider = rider;
        _outboundPipeline = outboundPipeline;
        _executionContext = executionContext;
    }

    public async Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
    {
        var context = new OutboundContext
        {
            Message = notification,
            ExecutionContext = _executionContext
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            await _rider.PublishAsync(notification, _executionContext, ct);
        }, cancellationToken);
    }
}
