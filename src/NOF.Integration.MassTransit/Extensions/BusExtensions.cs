using MassTransit;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NOF;

public static partial class __NOF_Infrastructure_MassTransit_Extensions__
{
    internal static class ValueCommandCache<TResult> where TResult : class, IResult
    {
        public static readonly ConcurrentDictionary<Type, Func<IBus, object, Uri, CancellationToken, Task<TResult>>> Cache = [];
    }

    extension(IBus bus)
    {
        public async Task<Result> SendAsync(ICommand command, Uri destinationAddress, CancellationToken cancellationToken = default)
        {
            var commandType = command.GetType();
            var cache = ValueCommandCache<Result>.Cache;
            var executor = cache.GetOrAdd(commandType, CreateExecutor<Result>);
            return await executor(bus, command, destinationAddress, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, Uri destinationAddress, CancellationToken cancellationToken = default)
        {
            var commandType = command.GetType();
            var cache = ValueCommandCache<Result<TResponse>>.Cache;
            var executor = cache.GetOrAdd(commandType, CreateExecutor<Result<TResponse>>);
            return await executor(bus, command, destinationAddress, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static class SendAsyncDelegate<TCommand, TResult>
        where TCommand : class
        where TResult : class, IResult
    {
        internal static async Task<TResult> Invoke(IBus bus, TCommand command, Uri destinationAddress, CancellationToken cancellationToken)
        {
            var response = await bus.Request<TCommand, TResult>(destinationAddress, command, cancellationToken).ConfigureAwait(false);
            return response.Message;
        }
    }

    internal static Func<IBus, object, Uri, CancellationToken, Task<TResult>> CreateExecutor<TResult>(Type commandType)
        where TResult : class, IResult
    {
        var bus = Expression.Parameter(typeof(IBus));
        var cmd = Expression.Parameter(typeof(object));
        var uri = Expression.Parameter(typeof(Uri));
        var token = Expression.Parameter(typeof(CancellationToken));

        var type = typeof(SendAsyncDelegate<,>).MakeGenericType(commandType, typeof(TResult));
        var method = type.GetMethod(nameof(SendAsyncDelegate<,>.Invoke), BindingFlags.Static | BindingFlags.NonPublic)!;
        var call = Expression.Call(method, bus, Expression.Convert(cmd, commandType), uri, token);
        return Expression.Lambda<Func<IBus, object, Uri, CancellationToken, Task<TResult>>>(call, bus, cmd, uri, token).Compile();
    }
}
