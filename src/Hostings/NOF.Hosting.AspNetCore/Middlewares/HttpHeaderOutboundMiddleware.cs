using Microsoft.AspNetCore.Http;

namespace NOF.Hosting.AspNetCore;

/// <summary>
/// Outbound middleware step that populates <see cref="OutboundContext.Headers"/> from HTTP request headers.
/// Runs before <see cref="TracingOutboundMiddleware"/> so that internal headers (tracing, tenant, etc.)
/// written by later middleware can take precedence over raw HTTP headers.
/// </summary>
public sealed class HttpHeaderOutboundMiddleware : IOutboundMiddleware, IBefore<TracingOutboundMiddleware>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpHeaderOutboundMiddleware(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            foreach (var header in httpContext.Request.Headers)
            {
                // Caller-provided headers take precedence (do not overwrite existing outbound headers).
                if (!context.Headers.ContainsKey(header.Key))
                {
                    context.Headers[header.Key] = header.Value.ToString();
                }
            }
        }

        return next(cancellationToken);
    }
}
