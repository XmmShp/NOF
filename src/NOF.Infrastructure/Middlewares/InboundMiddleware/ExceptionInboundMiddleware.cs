using Microsoft.Extensions.Logging;
using NOF.Contract;
using NOF.Domain;

namespace NOF.Infrastructure;

public sealed class ExceptionInboundMiddleware : AllMessagesInboundMiddleware
{
    private readonly ILogger<ExceptionInboundMiddleware> _logger;

    public ExceptionInboundMiddleware(ILogger<ExceptionInboundMiddleware> logger)
    {
        _logger = logger;
    }

    protected override async ValueTask InvokeAsyncCore(MessageInboundContext context, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken)
    {
        try
        {
            await next(cancellationToken);
        }
        catch (DomainException ex)
        {
            var handlerName = context.HandlerName;
            var messageName = context.MessageName ?? "<null>";
            _logger.LogWarning(ex, "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);
            context.Response = Result.Fail(ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            var handlerName = context.HandlerName;
            var messageName = context.MessageName ?? "<null>";
            _logger.LogError(ex, "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);
            context.Response = Result.Fail("500", "Internal server error");
        }
    }
}
