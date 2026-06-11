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

    public async ValueTask InvokeAsync(CommandInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        try
        {
            await next(cancellationToken);
        }
        catch (DomainException ex)
        {
            var handlerName = context.HandlerType.DisplayName;
            var messageName = context.Message.GetType().DisplayName;
            _logger.LogWarning(ex, "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);
        }
        catch (Exception ex)
        {
            var handlerName = context.HandlerType.DisplayName;
            var messageName = context.Message.GetType().DisplayName;
            _logger.LogError(ex, "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);
        }
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        try
        {
            await next(cancellationToken);
        }
        catch (DomainException ex)
        {
            var handlerName = context.HandlerType.DisplayName;
            var messageName = context.Message.GetType().DisplayName;
            _logger.LogWarning(ex, "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);
        }
        catch (Exception ex)
        {
            var handlerName = context.HandlerType.DisplayName;
            var messageName = context.Message.GetType().DisplayName;
            _logger.LogError(ex, "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);
        }
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        try
        {
            await next(cancellationToken);
        }
        catch (DomainException ex)
        {
            var handlerName = context.HandlerType.DisplayName;
            var requestName = $"{context.ServiceType.DisplayName}.{context.MethodName}";
            _logger.LogWarning(ex, "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                requestName, handlerName, ex.Message);
            context.Response = RpcResults.Fail(ParseStatusCode(ex.ErrorCode, 500), ex.Message);
        }
        catch (Exception ex)
        {
            var handlerName = context.HandlerType.DisplayName;
            var requestName = $"{context.ServiceType.DisplayName}.{context.MethodName}";
            _logger.LogError(ex, "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                requestName, handlerName, ex.Message);
            context.Response = RpcResults.Fail(500, "Internal server error");
        }
    }

    private static int ParseStatusCode(string? errorCode, int fallbackStatusCode)
        => int.TryParse(errorCode, out var statusCode) ? statusCode : fallbackStatusCode;
}
