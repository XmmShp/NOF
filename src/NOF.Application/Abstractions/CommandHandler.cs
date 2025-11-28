using MassTransit;

namespace NOF;

[ExcludeFromTopology]
public interface ICommandHandler;

[ExcludeFromTopology]
public interface IAsyncCommandHandler<TCommand> : IConsumer<TCommand>, ICommandHandler
    where TCommand : class, IAsyncCommand
{
    Task HandleAsync(ConsumeContext<TCommand> context);
}

[ExcludeFromTopology]
public abstract class AsyncCommandHandler<TCommand> : IAsyncCommandHandler<TCommand>
    where TCommand : class, IAsyncCommand
{
    public abstract Task HandleAsync(ConsumeContext<TCommand> context);
    public async Task Consume(ConsumeContext<TCommand> context)
    {
        await HandleAsync(context);
    }
}

[ExcludeFromTopology]
public interface ICommandHandler<TCommand> : IConsumer<TCommand>, ICommandHandler
    where TCommand : class, ICommand
{
    Task<Result> HandleAsync(ConsumeContext<TCommand> context);
}

[ExcludeFromTopology]
public abstract class CommandHandler<TCommand> : ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    public abstract Task<Result> HandleAsync(ConsumeContext<TCommand> context);
    public async Task Consume(ConsumeContext<TCommand> context)
    {
        var response = await HandleAsync(context);
        await context.RespondAsync(response).ConfigureAwait(false);
    }
}

[ExcludeFromTopology]
public interface ICommandHandler<TCommand, TResponse> : IConsumer<TCommand>, ICommandHandler
    where TCommand : class, ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(ConsumeContext<TCommand> context);
}

[ExcludeFromTopology]
public abstract class CommandHandler<TCommand, TResponse> : ICommandHandler<TCommand, TResponse>
    where TCommand : class, ICommand<TResponse>
{
    public abstract Task<Result<TResponse>> HandleAsync(ConsumeContext<TCommand> context);
    public async Task Consume(ConsumeContext<TCommand> context)
    {
        var response = await HandleAsync(context);
        await context.RespondAsync(response).ConfigureAwait(false);
    }
}