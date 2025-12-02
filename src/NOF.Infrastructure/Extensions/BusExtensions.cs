using MassTransit;

namespace NOF;

public static class BusExtensions
{
    extension(IBus bus)
    {
        public Task<Result> SendRequestAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
            where TCommand : class, ICommand
            => bus.SendRequestAsync(command, command.GetQueueUri(), cancellationToken);

        public async Task<Result> SendRequestAsync<TCommand>(TCommand command, Uri destinationAddress, CancellationToken cancellationToken = default)
            where TCommand : class, ICommand
        {
            var response = await bus.Request<TCommand, Result>(destinationAddress, command, cancellationToken);
            return response.Message;
        }

        public Task<Result<TResponse>> SendRequestAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken = default)
            where TCommand : class, ICommand<TResponse>
            => bus.SendRequestAsync<TCommand, TResponse>(command, command.GetQueueUri(), cancellationToken);

        public async Task<Result<TResponse>> SendRequestAsync<TCommand, TResponse>(TCommand command, Uri destinationAddress, CancellationToken cancellationToken = default)
            where TCommand : class, ICommand<TResponse>
        {
            var response = await bus.Request<TCommand, Result<TResponse>>(destinationAddress, command, cancellationToken);
            return response.Message;
        }
    }
}
