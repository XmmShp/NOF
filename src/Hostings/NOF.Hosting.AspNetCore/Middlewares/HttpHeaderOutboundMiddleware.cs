using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NOF.Contract;
using NOF.Infrastructure.Abstraction;
using NOF.Infrastructure.Core;

namespace NOF.Hosting.AspNetCore;

/// <summary>
/// Outbound middleware step that populates <see cref="OutboundContext.Headers"/> from HTTP request headers.
/// Runs before <see cref="TracingOutboundMiddlewareStep"/> so that internal headers (tracing, tenant, etc.)
/// written by later middleware take precedence over raw HTTP headers.
/// <para>
/// A configurable blacklist of wildcard patterns prevents external HTTP callers from forging
/// internal headers (e.g., <c>NOF.*</c>). These headers are only trusted when set by
/// internal service-to-service calls via the message bus.
/// </para>
/// </summary>
public class HttpHeaderOutboundMiddlewareStep : IOutboundMiddlewareStep<HttpHeaderOutboundMiddleware>,
    IBefore<TracingOutboundMiddlewareStep>;

/// <summary>
/// Configuration options for <see cref="HttpHeaderOutboundMiddleware"/>.
/// </summary>
public class HttpHeaderOutboundMiddlewareOptions
{
    /// <summary>
    /// Wildcard patterns for headers that should be blocked from external HTTP requests.
    /// Supports <c>*</c> wildcards (e.g., <c>NOF.*</c>, <c>X-Internal-*</c>).
    /// Matching is case-insensitive.
    /// </summary>
    public List<string> BlacklistedPatterns { get; set; } = ["NOF.*"];
}

/// <summary>
/// Copies HTTP request headers into <see cref="OutboundContext.Headers"/>,
/// filtering out blacklisted headers (matched by wildcard patterns) to prevent request forgery.
/// </summary>
public sealed class HttpHeaderOutboundMiddleware : IOutboundMiddleware
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HttpHeaderOutboundMiddlewareOptions _options;

    public HttpHeaderOutboundMiddleware(IHttpContextAccessor httpContextAccessor, IOptions<HttpHeaderOutboundMiddlewareOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            foreach (var header in httpContext.Request.Headers)
            {
                if (IsBlacklisted(header.Key))
                {
                    continue;
                }

                // Caller-provided headers take precedence
                if (!context.Headers.ContainsKey(header.Key))
                {
                    context.Headers[header.Key] = header.Value.ToString();
                }
            }
        }

        return next(cancellationToken);
    }

    private bool IsBlacklisted(string headerName)
    {
        foreach (var pattern in _options.BlacklistedPatterns)
        {
            if (headerName.MatchWildcard(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
