using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Domain;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class InboundExceptionMiddleware : ICommandInboundMiddleware, INotificationInboundMiddleware, IRequestInboundMiddleware
{
    private readonly ILogger<InboundExceptionMiddleware> _logger;

    public InboundExceptionMiddleware(ILogger<InboundExceptionMiddleware> logger)
    {
        _logger = logger;
    }

    public async ValueTask InvokeAsync(CommandInboundContext context, object message, CommandHandlerDelegate next, CancellationToken cancellationToken)
    {
        try
        {
            await next(context, message, cancellationToken);
        }
        catch (DomainException ex)
        {
            var handlerName = context.HandlerType.DisplayName;
            var messageName = context.MessageType.DisplayName;
            _logger.LogWarning(ex, "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);
        }
        catch (Exception ex)
        {
            var handlerName = context.HandlerType.DisplayName;
            var messageName = context.MessageType.DisplayName;
            _logger.LogError(ex, "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);
        }
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, object message, NotificationHandlerDelegate next, CancellationToken cancellationToken)
    {
        try
        {
            await next(context, message, cancellationToken);
        }
        catch (DomainException ex)
        {
            var handlerName = context.HandlerType.DisplayName;
            var messageName = context.MessageType.DisplayName;
            _logger.LogWarning(ex, "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);
        }
        catch (Exception ex)
        {
            var handlerName = context.HandlerType.DisplayName;
            var messageName = context.MessageType.DisplayName;
            _logger.LogError(ex, "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);
        }
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, object request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        try
        {
            await next(context, request, cancellationToken);
        }
        catch (DomainException ex)
        {
            var handlerName = context.HandlerType.DisplayName;
            var requestName = $"{context.ServiceType.DisplayName}.{context.ServiceMethodInfo.Name}";
            _logger.LogWarning(ex, "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                requestName, handlerName, ex.Message);
            context.Response = RequestInboundResponseFactory.CreateFailure(context, Result.Fail(ex.ErrorCode, ex.Message), 500);
        }
        catch (Exception ex)
        {
            var handlerName = context.HandlerType.DisplayName;
            var requestName = $"{context.ServiceType.DisplayName}.{context.ServiceMethodInfo.Name}";
            _logger.LogError(ex, "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                requestName, handlerName, ex.Message);
            context.Response = RequestInboundResponseFactory.CreateFailure(context, Result.Fail("500", "Internal server error"));
        }
    }
}
