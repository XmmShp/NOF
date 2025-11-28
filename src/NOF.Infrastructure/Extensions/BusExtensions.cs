using MassTransit;

namespace NOF;

public static class BusExtensions
{
    extension(IBus bus)
    {
        public Task<Result> SendRequestAsync(ICommand command, CancellationToken cancellationToken = default)
            => bus.SendRequestAsync(command, command.GetQueueUri(), cancellationToken);

        public async Task<Result> SendRequestAsync(ICommand command, Uri destinationAddress, CancellationToken cancellationToken = default)
        {
            var response = await bus.Request<ICommand, Result>(destinationAddress, command as object, cancellationToken);
            return response.Message;
        }

        public Task<Result<TResponse>> SendRequestAsync<TResponse>(ICommand<TResponse> command,
            CancellationToken cancellationToken = default)
            => bus.SendRequestAsync(command, command.GetQueueUri(), cancellationToken);

        public async Task<Result<TResponse>> SendRequestAsync<TResponse>(ICommand<TResponse> command, Uri destinationAddress,
            CancellationToken cancellationToken = default)
        {
            var response = await bus.Request<ICommand, Result<TResponse>>(destinationAddress, command as object, cancellationToken);
            return response.Message;
        }
    }
}
