using MassTransit;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NOF;

public static partial class __NOF_Infrastructure_Extensions__
{
    internal static class ValueCommandCache<TResult> where TResult : class, IResult
    {
        public static readonly ConcurrentDictionary<Type, Func<IBus, object, Uri, CancellationToken, Task<TResult>>> Cache = [];
    }

    extension(IBus bus)
    {
        public Task<Result> SendRequestAsync(ICommand command, CancellationToken cancellationToken = default)
            => bus.SendRequestAsync(command, command.GetQueueUri(), cancellationToken);

        public async Task<Result> SendRequestAsync(ICommand command, Uri destinationAddress, CancellationToken cancellationToken = default)
        {
            var commandType = command.GetType();
            var cache = ValueCommandCache<Result>.Cache;
            var executor = cache.GetOrAdd(commandType, _ => CreateExecutor<Result>(commandType));
            return await executor(bus, command, destinationAddress, cancellationToken).ConfigureAwait(false);
        }

        public Task<Result<TResponse>> SendRequestAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
            => bus.SendRequestAsync(command, command.GetQueueUri(), cancellationToken);

        public async Task<Result<TResponse>> SendRequestAsync<TResponse>(ICommand<TResponse> command, Uri destinationAddress, CancellationToken cancellationToken = default)
        {
            var commandType = command.GetType();
            var cache = ValueCommandCache<Result<TResponse>>.Cache;
            var executor = cache.GetOrAdd(commandType, _ => CreateExecutor<Result<TResponse>>(commandType));
            return await executor(bus, command, destinationAddress, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static Func<IBus, object, Uri, CancellationToken, Task<TResult>> CreateExecutor<TResult>(Type commandType)
        where TResult : class, IResult
    {
        var bus = Expression.Parameter(typeof(IBus));
        var cmd = Expression.Parameter(typeof(object));
        var uri = Expression.Parameter(typeof(Uri));
        var token = Expression.Parameter(typeof(CancellationToken));

        var method = SendRequestAsyncMethodInfo.MakeGenericMethod(commandType, typeof(TResult));

        var call = Expression.Call(method, bus, Expression.Convert(cmd, commandType), uri, token);
        return Expression.Lambda<Func<IBus, object, Uri, CancellationToken, Task<TResult>>>(call, bus, cmd, uri, token).Compile();
    }

    internal static MethodInfo SendRequestAsyncMethodInfo = typeof(__NOF_Infrastructure_Extensions__).GetMethod(nameof(SendRequestAsync), BindingFlags.Static | BindingFlags.NonPublic)!;
    internal static async Task<TResult> SendRequestAsync<TCommand, TResult>(IBus bus, TCommand command, Uri destinationAddress,
        CancellationToken cancellationToken = default)
        where TCommand : class
        where TResult : class, IResult
    {
        var response = await bus.Request<TCommand, TResult>(destinationAddress, command, cancellationToken).ConfigureAwait(false);
        return response.Message;
    }
}
