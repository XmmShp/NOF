using MassTransit;
using MassTransit.Mediator;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NOF;

public class MassTransitRequestSender : IRequestSender
{
    private readonly IRequestHandleNodeFactory _factory;

    public MassTransitRequestSender(IRequestHandleNodeFactory factory)
    {
        _factory = factory;
    }

    public Task<Result> SendAsync(IRequest request, string? destinationEndpointName = null, CancellationToken cancellationToken = default)
    {
        return _factory.SendAsync(request, destinationEndpointName, cancellationToken);
    }

    public Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, string? destinationEndpointName = null, CancellationToken cancellationToken = default)
    {
        return _factory.SendAsync(request, destinationEndpointName, cancellationToken);
    }
}

public interface IRequestHandleNodeRegistry
{
    LinkedList<Type> Registry { get; }
}

public class RequestHandleNodeRegistry : IRequestHandleNodeRegistry
{
    public LinkedList<Type> Registry { get; } = [];
}

public interface IRequestHandleNodeFactory : IRequestSender;

public interface IRequestHandleNode : IRequestSender
{
    /// <summary>
    /// Determines whether this handler can process the given request and endpoint.
    /// Called once per handler during chain execution.
    /// </summary>
    bool CanHandle(Type requestType, string? destinationEndpointName);
}

internal class MediatorRequestHandleNode : IRequestHandleNode
{
    private readonly IScopedMediator _mediator;
    internal static readonly HashSet<Type> SupportedRequestTypes = [];
    public MediatorRequestHandleNode(IScopedMediator mediator)
    {
        _mediator = mediator;
    }

    public bool CanHandle(Type requestType, string? destinationEndpointName)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        if (destinationEndpointName is not null && destinationEndpointName != "loopback://localhost/")
        {
            return false;
        }
        return SupportedRequestTypes.Contains(requestType);
    }

    public Task<Result> SendAsync(IRequest request, string? destinationEndpointName, CancellationToken cancellationToken)
    {
        return _mediator.SendRequest(request, cancellationToken);
    }

    public Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, string? destinationEndpointName, CancellationToken cancellationToken)
    {
        return _mediator.SendRequest(request, cancellationToken);
    }
}

internal class RiderRequestHandleNode : IRequestHandleNode
{
    internal static class ValueCommandCache<TResult> where TResult : class, IResult
    {
        public static readonly ConcurrentDictionary<Type, Func<IScopedClientFactory, IRequestBase, Uri, CancellationToken, Task<TResult>>> Cache = [];
    }

    private readonly IScopedClientFactory _clientFactory;
    private readonly IEndpointNameProvider _nameProvider;

    public RiderRequestHandleNode(IScopedClientFactory clientFactory, IEndpointNameProvider nameProvider)
    {
        _clientFactory = clientFactory;
        _nameProvider = nameProvider;
    }

    public bool CanHandle(Type requestType, string? destinationEndpointName) => true;

    public async Task<Result> SendAsync(IRequest request, string? destinationEndpointName, CancellationToken cancellationToken)
    {
        destinationEndpointName ??= _nameProvider.GetEndpointName(request.GetType());
        var commandType = request.GetType();
        var cache = ValueCommandCache<Result>.Cache;
        var executor = cache.GetOrAdd(commandType, _ => CreateExecutor<Result>(commandType));
        return await executor(_clientFactory, request, destinationEndpointName.ToQueueUri(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, string? destinationEndpointName, CancellationToken cancellationToken)
    {
        destinationEndpointName ??= _nameProvider.GetEndpointName(request.GetType());
        var commandType = request.GetType();
        var cache = ValueCommandCache<Result<TResponse>>.Cache;
        var executor = cache.GetOrAdd(commandType, _ => CreateExecutor<Result<TResponse>>(commandType));
        return await executor(_clientFactory, request, destinationEndpointName.ToQueueUri(), cancellationToken).ConfigureAwait(false);
    }

    #region Reflection Helper
    internal static Func<IScopedClientFactory, IRequestBase, Uri, CancellationToken, Task<TResult>> CreateExecutor<TResult>(Type requestType)
        where TResult : IResult
    {
        var client = Expression.Parameter(typeof(IScopedClientFactory));
        var request = Expression.Parameter(typeof(IRequestBase));
        var uri = Expression.Parameter(typeof(Uri));
        var token = Expression.Parameter(typeof(CancellationToken));

        var method = SendRequestAsyncMethodInfo.MakeGenericMethod(requestType, typeof(TResult));

        var call = Expression.Call(method, client, Expression.Convert(request, requestType), uri, token);
        return Expression.Lambda<Func<IScopedClientFactory, IRequestBase, Uri, CancellationToken, Task<TResult>>>(call, client, request, uri, token).Compile();
    }

    internal static readonly MethodInfo SendRequestAsyncMethodInfo = typeof(RiderRequestHandleNode).GetMethod(nameof(SendRequestAsync), BindingFlags.Static | BindingFlags.NonPublic)!;
    internal static async Task<TResult> SendRequestAsync<TCommand, TResult>(IScopedClientFactory client, TCommand command, Uri destinationAddress, CancellationToken cancellationToken)
        where TCommand : class
        where TResult : class
    {
        var requestHandle = client.CreateRequest(destinationAddress, command, cancellationToken);
        var response = await requestHandle.GetResponse<TResult>();
        return response.Message;
    }
    #endregion
}

internal class RequestHandleNodeFactory : IRequestHandleNodeFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IRequestHandleNodeRegistry _registry;
    private readonly ConcurrentDictionary<(Type requestType, string? destinationEndpointName), Type> _cache = [];
    public RequestHandleNodeFactory(IRequestHandleNodeRegistry registry, IServiceProvider serviceProvider)
    {
        _registry = registry;
        _serviceProvider = serviceProvider;
    }

    public bool CanHandle(Type requestType, string? destinationEndpointName)
    {
        if (_cache.ContainsKey((requestType, destinationEndpointName)))
        {
            return true;
        }
        foreach (var nodeType in _registry.Registry)
        {
            var node = (IRequestHandleNode)ActivatorUtilities.CreateInstance(_serviceProvider, nodeType);
            if (!node.CanHandle(requestType, destinationEndpointName))
            {
                continue;
            }
            _cache.GetOrAdd((requestType, destinationEndpointName), nodeType);
            return true;
        }
        return false;
    }
    public async Task<Result> SendAsync(IRequest request, string? destinationEndpointName, CancellationToken cancellationToken = default)
    {
        var keyPair = (type: request.GetType(), destinationEndpointName);

        if (!CanHandle(keyPair.type, keyPair.destinationEndpointName))
        {
            throw new InvalidOperationException("No handler found for the given request.");
        }

        var nodeType = _cache[(request.GetType(), destinationEndpointName)];
        var node = (IRequestHandleNode)ActivatorUtilities.CreateInstance(_serviceProvider, nodeType);
        return await node.SendAsync(request, destinationEndpointName, cancellationToken).ConfigureAwait(false);
    }
    public async Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, string? destinationEndpointName, CancellationToken cancellationToken = default)
    {
        var keyPair = (type: request.GetType(), destinationEndpointName);

        if (!CanHandle(keyPair.type, keyPair.destinationEndpointName))
        {
            throw new InvalidOperationException("No handler found for the given request.");
        }

        var nodeType = _cache[(request.GetType(), destinationEndpointName)];
        var node = (IRequestHandleNode)ActivatorUtilities.CreateInstance(_serviceProvider, nodeType);
        return await node.SendAsync(request, destinationEndpointName, cancellationToken).ConfigureAwait(false);
    }
}
