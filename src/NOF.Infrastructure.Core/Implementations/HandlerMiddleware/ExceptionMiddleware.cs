using Microsoft.Extensions.Logging;
using NOF.Contract;
using NOF.Domain;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Handler exception handling middleware
/// Catches and converts exceptions to unified error responses
/// </summary>
public sealed class ExceptionMiddleware : IHandlerMiddleware
{
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(ILogger<ExceptionMiddleware> logger)
    {
        _logger = logger;
    }

    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        try
        {
            await next(cancellationToken);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                context.MessageType, context.HandlerType, ex.Message);

            // Convert domain exception to error response with specific error code and message
            var errorResult = Result.Fail(ex.ErrorCode, ex.Message);

            // Set the error response to context
            context.Response = errorResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                context.MessageType, context.HandlerType, ex.Message);

            // Convert exception to unified error response
            var errorResult = Result.Fail(500, "Internal server error");

            // Set the error response to context
            context.Response = errorResult;
        }
    }
}
