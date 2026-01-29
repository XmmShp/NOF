using Microsoft.Extensions.Logging;

namespace NOF;

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while handling {MessageType} with {HandlerType}: {Message}",
                context.MessageType, context.HandlerType, ex.Message);

            // Convert exception to unified error response
            var errorResult = new Result
            {
                IsSuccess = false,
                ErrorCode = 500,
                Message = "Internal server error"
            };

            // Set the error response to context
            context.Response = errorResult;
        }
    }
}
