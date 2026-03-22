using MassTransit;
using MassTransit.Mediator;
using NOF.Contract;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NOF.Infrastructure.MassTransit;

/// <summary>
/// MassTransit request transport implementation.
/// Dispatches locally via mediator when the request type has a local handler
/// and the destination endpoint is null/whitespace or matches the local endpoint.
/// Otherwise dispatches remotely via the bus 鈥?requires explicit <c>destinationEndpointName</c>.
/// </summary>
public class MassTransitRequestRider : IRequestRider
{
    private readonly IScopedMediator _mediator;
    private readonly IScopedClientFactory _clientFactory;
    private readonly LocalHandlerRegistry _localHandlers;

    public MassTransitRequestRider(
        IScopedMediator mediator,
        IScopedClientFactory clientFactory,
        LocalHandlerRegistry localHandlers)
    {
        _mediator = mediator;
        _clientFactory = clientFactory;
        _localHandlers = localHandlers;
    }

    public Task<Result> SendAsync(IRequest request, IDictionary<string, string?>? headers = null, string? destinationEndpointName = null, CancellationToken cancellationToken = default)
    {
        if (_localHandlers.ShouldDispatchLocally(request.GetType(), destinationEndpointName))
        {
            return _mediator.SendRequest(request, headers, cancellationToken: cancellationToken);
        }

        return SendRemoteAsync<Result>(request, headers, destinationEndpointName, cancellationToken);
    }

    public Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, IDictionary<string, string?>? headers = null, string? destinationEndpointName = null, CancellationToken cancellationToken = default)
    {
        if (_localHandlers.ShouldDispatchLocally(request.GetType(), destinationEndpointName))
        {
            return _mediator.SendRequest(request, headers, cancellationToken: cancellationToken);
        }

        return SendRemoteAsync<Result<TResponse>>(request, headers, destinationEndpointName, cancellationToken);
    }

    #region Remote dispatch helpers

    private static class RemoteExecutorCache<TResult> where TResult : class, IResult
    {
        public static readonly ConcurrentDictionary<Type, Func<IScopedClientFactory, IRequestMarker, IDictionary<string, string?>?, Uri, CancellationToken, Task<TResult>>> Cache = [];
    }

    private async Task<TResult> SendRemoteAsync<TResult>(IRequestMarker request, IDictionary<string, string?>? headers, string? destinationEndpointName, CancellationToken cancellationToken)
        where TResult : class, IResult
    {
        if (string.IsNullOrWhiteSpace(destinationEndpointName))
        {
            throw new InvalidOperationException(
                $"Remote dispatch of request '{request.GetType().Name}' requires an explicit destinationEndpointName. " +
                "No local handler is registered for this request type.");
        }

        var requestType = request.GetType();
        var executor = RemoteExecutorCache<TResult>.Cache.GetOrAdd(requestType, CreateRemoteExecutor<TResult>);
        return await executor(_clientFactory, request, headers, destinationEndpointName.ToQueueUri(), cancellationToken).ConfigureAwait(false);
    }

    private static Func<IScopedClientFactory, IRequestMarker, IDictionary<string, string?>?, Uri, CancellationToken, Task<TResult>> CreateRemoteExecutor<TResult>(Type requestType)
        where TResult : class, IResult
    {
        var client = Expression.Parameter(typeof(IScopedClientFactory));
        var request = Expression.Parameter(typeof(IRequestMarker));
        var hdrs = Expression.Parameter(typeof(IDictionary<string, string?>));
        var uri = Expression.Parameter(typeof(Uri));
        var token = Expression.Parameter(typeof(CancellationToken));

        var method = typeof(MassTransitRequestRider)
            .GetMethod(nameof(SendRemoteTypedAsync), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(requestType, typeof(TResult));

        var call = Expression.Call(method, client, Expression.Convert(request, requestType), hdrs, uri, token);
        return Expression.Lambda<Func<IScopedClientFactory, IRequestMarker, IDictionary<string, string?>?, Uri, CancellationToken, Task<TResult>>>(call, client, request, hdrs, uri, token).Compile();
    }

    private static async Task<TResult> SendRemoteTypedAsync<TCommand, TResult>(IScopedClientFactory client, TCommand command, IDictionary<string, string?>? headers, Uri destinationAddress, CancellationToken cancellationToken)
        where TCommand : class
        where TResult : class
    {
        var requestHandle = client.CreateRequest(destinationAddress, command, cancellationToken);

        requestHandle.UseExecute(context => context.ApplyHeaders(headers));

        var response = await requestHandle.GetResponse<TResult>();
        return response.Message;
    }

    #endregion
}
