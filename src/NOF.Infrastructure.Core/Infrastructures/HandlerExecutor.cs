using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NOF;

/// <summary>
/// Handler executor interface
/// Responsible for executing handlers through the pipeline
/// </summary>
public interface IHandlerExecutor
{
    /// <summary>
    /// Execute Command Handler
    /// </summary>
    ValueTask ExecuteCommandAsync<TCommand>(
        ICommandHandler<TCommand> handler,
        TCommand command,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TCommand : class, ICommand;

    /// <summary>
    /// Execute Notification Handler
    /// </summary>                  
    ValueTask ExecuteNotificationAsync<TNotification>(
        INotificationHandler<TNotification> handler,
        TNotification notification,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TNotification : class, INotification;

    /// <summary>
    /// Execute Request Handler (no return value)
    /// </summary>
    ValueTask<Result> ExecuteRequestAsync<TRequest>(
        IRequestHandler<TRequest> handler,
        TRequest request,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TRequest : class, IRequest;

    /// <summary>
    /// Execute Request Handler (with return value)
    /// </summary>
    ValueTask<Result<TResponse>> ExecuteRequestAsync<TRequest, TResponse>(
        IRequestHandler<TRequest, TResponse> handler,
        TRequest request,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TRequest : class, IRequest<TResponse>;
}

/// <summary>
/// Default implementation of Handler executor
/// </summary>
public sealed class HandlerExecutor : IHandlerExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<Action<IHandlerPipelineBuilder, IServiceProvider>> _configureActions;

    public HandlerExecutor(
        IServiceProvider serviceProvider,
        IEnumerable<Action<IHandlerPipelineBuilder, IServiceProvider>> configureActions)
    {
        _serviceProvider = serviceProvider;
        _configureActions = configureActions.ToList();
    }

    public async ValueTask ExecuteCommandAsync<TCommand>(
        ICommandHandler<TCommand> handler,
        TCommand command,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TCommand : class, ICommand
    {
        var context = new HandlerContext
        {
            Message = command,
            Handler = handler
        };

        var pipeline = BuildPipeline(context, headers, ct => new ValueTask(handler.HandleAsync(command, ct)));
        await pipeline(cancellationToken);
    }

    public async ValueTask ExecuteNotificationAsync<TNotification>(
        INotificationHandler<TNotification> handler,
        TNotification notification,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TNotification : class, INotification
    {
        var context = new HandlerContext
        {
            Message = notification,
            Handler = handler
        };

        var pipeline = BuildPipeline(context, headers, ct => new ValueTask(handler.HandleAsync(notification, ct)));
        await pipeline(cancellationToken);
    }

    public async ValueTask<Result> ExecuteRequestAsync<TRequest>(
        IRequestHandler<TRequest> handler,
        TRequest request,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TRequest : class, IRequest
    {
        var context = new HandlerContext
        {
            Message = request,
            Handler = handler
        };

        var pipeline = BuildPipeline(context, headers, async ct =>
        {
            context.Response = await handler.HandleAsync(request, ct);
        });
        await pipeline(cancellationToken);

        return (Result)context.Response!;
    }

    public async ValueTask<Result<TResponse>> ExecuteRequestAsync<TRequest, TResponse>(
        IRequestHandler<TRequest, TResponse> handler,
        TRequest request,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TRequest : class, IRequest<TResponse>
    {
        var context = new HandlerContext
        {
            Message = request,
            Handler = handler
        };

        var pipeline = BuildPipeline(context, headers, async ct =>
        {
            context.Response = await handler.HandleAsync(request, ct);
        });
        await pipeline(cancellationToken);

        return (Result<TResponse>)context.Response!;
    }

    private HandlerDelegate BuildPipeline(HandlerContext context, IDictionary<string, string?> headers, HandlerDelegate handler)
    {
        // Copy headers to InvocationContext.Items for middleware access
        var invocationContext = _serviceProvider.GetRequiredService<IInvocationContextInternal>();
        foreach (var header in headers)
        {
            invocationContext.Items[header.Key] = header.Value;
        }

        // Set tracing information from headers to InvocationContext properties
        headers.TryGetValue(NOFConstants.TraceId, out var traceId);
        headers.TryGetValue(NOFConstants.SpanId, out var spanId);
        invocationContext.SetTracingInfo(traceId, spanId);

        var builder = new HandlerPipelineBuilder();

        // 0. Exception handling (outermost to catch all exceptions)
        var exceptionLogger = _serviceProvider.GetRequiredService<ILogger<ExceptionMiddleware>>();
        builder.Use(new ExceptionMiddleware(exceptionLogger));

        // 1. Permission authorization
        builder.Use(new PermissionAuthorizationMiddleware(
            invocationContext,
            _serviceProvider.GetRequiredService<ILogger<PermissionAuthorizationMiddleware>>()));

        // 2. Activity tracing
        builder.Use(new ActivityTracingMiddleware(invocationContext));

        // 3. Auto instrumentation
        var logger = _serviceProvider.GetRequiredService<ILogger<AutoInstrumentationMiddleware>>();
        builder.Use(new AutoInstrumentationMiddleware(logger));

        // 4. Tenant header processing
        builder.Use(new TenantHeaderMiddleware(invocationContext));

        // 5. Inbox message processing
        var transactionManager = _serviceProvider.GetRequiredService<ITransactionManager>();
        var inboxMessageRepository = _serviceProvider.GetRequiredService<IInboxMessageRepository>();
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var inboxLogger = _serviceProvider.GetRequiredService<ILogger<MessageInboxMiddleware>>();
        builder.Use(new MessageInboxMiddleware(transactionManager, inboxMessageRepository, unitOfWork, inboxLogger, invocationContext));

        // 6. Transactional message context
        var deferredCommandSender = _serviceProvider.GetRequiredService<IDeferredCommandSender>();
        var deferredNotificationPublisher = _serviceProvider.GetRequiredService<IDeferredNotificationPublisher>();
        builder.Use(new MessageOutboxContextMiddleware(deferredCommandSender, deferredNotificationPublisher));

        // 7. User-defined middleware extension points (can call Configure multiple times)
        foreach (var configure in _configureActions)
        {
            configure(builder, _serviceProvider);
        }

        return builder.Build(context, handler);
    }
}
