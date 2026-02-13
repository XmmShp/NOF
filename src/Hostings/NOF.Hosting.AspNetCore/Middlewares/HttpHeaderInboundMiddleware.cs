using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NOF.Contract;
using NOF.Infrastructure.Core;

namespace NOF.Hosting.AspNetCore;

/// <summary>
/// Handler middleware step that populates <see cref="InboundContext.Headers"/> from HTTP request headers.
/// Runs before <see cref="IdentityInboundMiddlewareStep"/> so that identity/tenant resolution can read them.
/// <para>
/// A configurable blacklist of wildcard patterns prevents external HTTP callers from forging
/// internal headers (e.g., <c>NOF.*</c>). These headers are only trusted when set by
/// internal service-to-service calls via the message bus.
/// </para>
/// </summary>
public class HttpHeaderInboundMiddlewareStep : IInboundMiddlewareStep<HttpHeaderInboundMiddleware>,
    IAfter<ExceptionInboundMiddlewareStep>, IBefore<IdentityInboundMiddlewareStep>;

/// <summary>
/// Configuration options for <see cref="HttpHeaderInboundMiddleware"/>.
/// </summary>
public class HttpHeaderInboundMiddlewareOptions
{
    /// <summary>
    /// Wildcard patterns for headers that should be blocked from external HTTP requests.
    /// Supports <c>*</c> wildcards (e.g., <c>NOF.*</c>, <c>X-Internal-*</c>).
    /// Matching is case-insensitive.
    /// </summary>
    public List<string> BlacklistedPatterns { get; set; } = ["NOF.*"];
}

/// <summary>
/// Copies HTTP request headers into <see cref="InboundContext.Headers"/>,
/// filtering out blacklisted headers (matched by wildcard patterns) to prevent request forgery.
/// </summary>
public sealed class HttpHeaderInboundMiddleware : IInboundMiddleware
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HttpHeaderInboundMiddlewareOptions _options;

    public HttpHeaderInboundMiddleware(IHttpContextAccessor httpContextAccessor, IOptions<HttpHeaderInboundMiddlewareOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public ValueTask InvokeAsync(InboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            foreach (var header in httpContext.Request.Headers)
            {
                if (IsBlacklisted(header.Key))
                    continue;

                // Caller-provided headers (e.g., from message bus) take precedence
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
                return true;
        }
        return false;
    }
}
