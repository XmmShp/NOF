using MassTransit;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NOF;

public static partial class NOFInfrastructureMassTransitExtensions
{
    internal static class ValueRequestCache<TResult> where TResult : class, IResult
    {
        public static readonly ConcurrentDictionary<Type, Func<IBus, object, Uri, CancellationToken, Task<TResult>>> Cache = [];
    }

    extension(IBus bus)
    {
        public async Task<Result> SendAsync(IRequest request, Uri destinationAddress, CancellationToken cancellationToken = default)
        {
            var requestType = request.GetType();
            var cache = ValueRequestCache<Result>.Cache;
            var executor = cache.GetOrAdd(requestType, CreateExecutor<Result>);
            return await executor(bus, request, destinationAddress, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, Uri destinationAddress, CancellationToken cancellationToken = default)
        {
            var requestType = request.GetType();
            var cache = ValueRequestCache<Result<TResponse>>.Cache;
            var executor = cache.GetOrAdd(requestType, CreateExecutor<Result<TResponse>>);
            return await executor(bus, request, destinationAddress, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static class SendAsyncDelegate<TRequest, TResult>
        where TRequest : class
        where TResult : class, IResult
    {
        internal static async Task<TResult> Invoke(IBus bus, TRequest request, Uri destinationAddress, CancellationToken cancellationToken)
        {
            var response = await bus.Request<TRequest, TResult>(destinationAddress, request, cancellationToken).ConfigureAwait(false);
            return response.Message;
        }
    }

    internal static Func<IBus, object, Uri, CancellationToken, Task<TResult>> CreateExecutor<TResult>(Type requestType)
        where TResult : class, IResult
    {
        var bus = Expression.Parameter(typeof(IBus));
        var cmd = Expression.Parameter(typeof(object));
        var uri = Expression.Parameter(typeof(Uri));
        var token = Expression.Parameter(typeof(CancellationToken));

        var type = typeof(SendAsyncDelegate<,>).MakeGenericType(requestType, typeof(TResult));
        var method = type.GetMethod(nameof(SendAsyncDelegate<,>.Invoke), BindingFlags.Static | BindingFlags.NonPublic)!;
        var call = Expression.Call(method, bus, Expression.Convert(cmd, requestType), uri, token);
        return Expression.Lambda<Func<IBus, object, Uri, CancellationToken, Task<TResult>>>(call, bus, cmd, uri, token).Compile();
    }
}
