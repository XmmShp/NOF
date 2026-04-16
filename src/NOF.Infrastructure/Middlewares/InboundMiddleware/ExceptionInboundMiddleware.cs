using Microsoft.Extensions.Logging;
using NOF.Contract;
using NOF.Domain;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class CommandExceptionInboundMiddleware : ICommandInboundMiddleware
{
    private readonly ILogger<CommandExceptionInboundMiddleware> _logger;

    public CommandExceptionInboundMiddleware(ILogger<CommandExceptionInboundMiddleware> logger)
    {
        _logger = logger;
    }

    public async ValueTask InvokeAsync(CommandInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        try
        {
            await next(cancellationToken);
        }
        catch (DomainException ex)
        {
            var handlerName = context.HandlerName;
            var messageName = context.MessageType.FullName ?? context.MessageType.Name;
            _logger.LogWarning(ex, "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);
            context.Response = Result.Fail(ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            var handlerName = context.HandlerName;
            var messageName = context.MessageType.FullName ?? context.MessageType.Name;
            _logger.LogError(ex, "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);
            context.Response = Result.Fail("500", "Internal server error");
        }
    }
}

public sealed class NotificationExceptionInboundMiddleware : INotificationInboundMiddleware
{
    private readonly ILogger<NotificationExceptionInboundMiddleware> _logger;

    public NotificationExceptionInboundMiddleware(ILogger<NotificationExceptionInboundMiddleware> logger)
    {
        _logger = logger;
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        try
        {
            await next(cancellationToken);
        }
        catch (DomainException ex)
        {
            var handlerName = context.HandlerName;
            var messageName = context.MessageType.FullName ?? context.MessageType.Name;
            _logger.LogWarning(ex, "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);
            context.Response = Result.Fail(ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            var handlerName = context.HandlerName;
            var messageName = context.MessageType.FullName ?? context.MessageType.Name;
            _logger.LogError(ex, "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);
            context.Response = Result.Fail("500", "Internal server error");
        }
    }
}

public sealed class RequestExceptionInboundMiddleware : IRequestInboundMiddleware
{
    private readonly ILogger<RequestExceptionInboundMiddleware> _logger;

    public RequestExceptionInboundMiddleware(ILogger<RequestExceptionInboundMiddleware> logger)
    {
        _logger = logger;
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        try
        {
            await next(cancellationToken);
        }
        catch (DomainException ex)
        {
            var handlerName = context.HandlerName;
            var requestName = $"{context.ServiceType.FullName ?? context.ServiceType.Name}.{context.MethodName}";
            _logger.LogWarning(ex, "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                requestName, handlerName, ex.Message);
            context.Response = Result.Fail(ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            var handlerName = context.HandlerName;
            var requestName = $"{context.ServiceType.FullName ?? context.ServiceType.Name}.{context.MethodName}";
            _logger.LogError(ex, "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                requestName, handlerName, ex.Message);
            context.Response = Result.Fail("500", "Internal server error");
        }
    }
}
