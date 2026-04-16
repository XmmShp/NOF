using Microsoft.AspNetCore.Http;

namespace NOF.Hosting.AspNetCore;

/// <summary>
/// Request outbound middleware that populates outbound headers from the current HTTP request.
/// Runs before <see cref="RequestTracingOutboundMiddleware"/> so later middleware can override framework headers.
/// </summary>
public sealed class HttpHeaderOutboundMiddleware : RequestOutboundMiddleware, IBefore<RequestTracingOutboundMiddleware>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpHeaderOutboundMiddleware(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            foreach (var header in httpContext.Request.Headers)
            {
                if (!context.Headers.ContainsKey(header.Key))
                {
                    context.Headers[header.Key] = header.Value.ToString();
                }
            }
        }

        return next(cancellationToken);
    }
}
