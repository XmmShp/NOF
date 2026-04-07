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
            _logger.LogWarning(ex, "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                context.Message.GetType().FullName, context.HandlerType.FullName, ex.Message);

            // Convert domain exception to error response with specific error code and message
            var errorResult = Result.Fail(ex.ErrorCode, ex.Message);

            // Set the error response to context
            context.Response = errorResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                context.Message.GetType().FullName, context.HandlerType.FullName, ex.Message);

            // Convert exception to unified error response
            var errorResult = Result.Fail("500", "Internal server error");

            // Set the error response to context
            context.Response = errorResult;
        }
    }
}

