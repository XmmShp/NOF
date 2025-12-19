using MassTransit;

namespace NOF;

public class CorrelationFilter<T> : IFilter<SendContext<T>>, IFilter<PublishContext<T>>
    where T : class
{
    public async Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        var current = context.CorrelationId;
        if (current is null)
        {
            context.CorrelationId = Guid.NewGuid();
        }
        await next.Send(context);
    }

    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        var current = context.CorrelationId;
        if (current is null)
        {
            context.CorrelationId = Guid.NewGuid();
        }
        await next.Send(context);
    }

    public void Probe(ProbeContext context) { }
}

