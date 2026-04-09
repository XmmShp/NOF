using Microsoft.Extensions.Logging;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;

namespace NOF.Infrastructure;

/// <summary>Outermost middleware step catches all exceptions.</summary>
/// <summary>
/// Handler exception handling middleware
/// Catches and converts exceptions to unified error responses
/// </summary>
public sealed class ExceptionInboundMiddleware : IInboundMiddleware
{
    private readonly ILogger<ExceptionInboundMiddleware> _logger;

    public ExceptionInboundMiddleware(ILogger<ExceptionInboundMiddleware> logger)
    {
        _logger = logger;
    }

    public async ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken)
    {
        try
        {
            await next(cancellationToken);
        }
        catch (DomainException ex)
        {
            var handlerType = context.Metadatas.TryGetValue("HandlerType", out var handlerTypeObj) && handlerTypeObj is Type type ? type : null;
            var handlerName = context.Metadatas.TryGetValue("HandlerName", out var hn) ? hn as string : handlerType?.FullName;
            var messageName = context.Metadatas.TryGetValue("MessageName", out var mn) ? mn as string : context.Message?.GetType().FullName ?? "<null>";
            _logger.LogWarning(ex, "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName, handlerName, ex.Message);

            // Convert domain exception to error response with specific error code and message
            var errorResult = Result.Fail(ex.ErrorCode, ex.Message);

            // Set the error response to context
            context.Response = errorResult;
        }
        catch (Exception ex)
        {
            var handlerType = context.Metadatas.TryGetValue("HandlerType", out var handlerTypeObj2) && handlerTypeObj2 is Type type2 ? type2 : null;
            var handlerName2 = context.Metadatas.TryGetValue("HandlerName", out var hn2) ? hn2 as string : handlerType?.FullName;
            var messageName2 = context.Metadatas.TryGetValue("MessageName", out var mn2) ? mn2 as string : context.Message?.GetType().FullName ?? "<null>";
            _logger.LogError(ex, "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                messageName2, handlerName2, ex.Message);

            // Convert exception to unified error response
            var errorResult = Result.Fail("500", "Internal server error");

            // Set the error response to context
            context.Response = errorResult;
        }
    }
}
