using MassTransit;

namespace NOF;

[ExcludeFromTopology]
public interface IRequestHandler;

[ExcludeFromTopology]
public abstract class RequestHandler<TRequest> : IConsumer<TRequest>, IRequestHandler
    where TRequest : class, IRequest
{
    public abstract Task<Result> HandleAsync(TRequest request, CancellationToken cancellationToken);
    public async Task Consume(ConsumeContext<TRequest> context)
    {
        var response = await HandleAsync(context.Message, context.CancellationToken).ConfigureAwait(false);
        await context.RespondAsync(response).ConfigureAwait(false);
    }
}

[ExcludeFromTopology]
public abstract class RequestHandler<TRequest, TResponse> : IConsumer<TRequest>, IRequestHandler
    where TRequest : class, IRequest<TResponse>
{
    public async Task Consume(ConsumeContext<TRequest> context)
    {
        var response = await HandleAsync(context.Message, context.CancellationToken).ConfigureAwait(false);
        await context.RespondAsync(response).ConfigureAwait(false);
    }

    public abstract Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}