using MassTransit;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NOF;

public class CommandSender : ICommandSender
{
    internal static class ValueCommandCache<TResult> where TResult : class, IResult
    {
        public static readonly ConcurrentDictionary<Type, Func<IScopedClientFactory, ICommandBase, Uri, CancellationToken, Task<TResult>>> Cache = [];
    }

    private readonly IScopedClientFactory _clientFactory;
    private readonly ISendEndpointProvider _sendEndpointProvider;

    public CommandSender(IScopedClientFactory clientFactory, ISendEndpointProvider sendEndpointProvider)
    {
        _clientFactory = clientFactory;
        _sendEndpointProvider = sendEndpointProvider;
    }

    public async Task<Result> SendAsync(ICommand command, string? destinationEndpointName, CancellationToken cancellationToken)
    {
        destinationEndpointName ??= command.GetType().GetEndpointName();
        var commandType = command.GetType();
        var cache = ValueCommandCache<Result>.Cache;
        var executor = cache.GetOrAdd(commandType, _ => CreateExecutor<Result>(commandType));
        return await executor(_clientFactory, command, destinationEndpointName.ToQueueUri(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, string? destinationEndpointName, CancellationToken cancellationToken)
    {
        destinationEndpointName ??= command.GetType().GetEndpointName();
        var commandType = command.GetType();
        var cache = ValueCommandCache<Result<TResponse>>.Cache;
        var executor = cache.GetOrAdd(commandType, _ => CreateExecutor<Result<TResponse>>(commandType));
        return await executor(_clientFactory, command, destinationEndpointName.ToQueueUri(), cancellationToken).ConfigureAwait(false);
    }

    public Task SendAsync(IAsyncCommand command, string? destinationEndpointName, CancellationToken cancellationToken)
    {
        destinationEndpointName ??= command.GetType().GetEndpointName();
        return _sendEndpointProvider.SendCommand(command, destinationEndpointName.ToQueueUri(), cancellationToken);
    }

    #region Reflection Helper
    internal static Func<IScopedClientFactory, ICommandBase, Uri, CancellationToken, Task<TResult>> CreateExecutor<TResult>(Type commandType)
        where TResult : IResult
    {
        var client = Expression.Parameter(typeof(IScopedClientFactory));
        var cmd = Expression.Parameter(typeof(ICommandBase));
        var uri = Expression.Parameter(typeof(Uri));
        var token = Expression.Parameter(typeof(CancellationToken));

        var method = SendRequestAsyncMethodInfo.MakeGenericMethod(commandType, typeof(TResult));

        var call = Expression.Call(method, client, Expression.Convert(cmd, commandType), uri, token);
        return Expression.Lambda<Func<IScopedClientFactory, ICommandBase, Uri, CancellationToken, Task<TResult>>>(call, client, cmd, uri, token).Compile();
    }

    internal static readonly MethodInfo SendRequestAsyncMethodInfo = typeof(CommandSender).GetMethod(nameof(SendRequestAsync), BindingFlags.Static | BindingFlags.NonPublic)!;
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