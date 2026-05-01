using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NOF.Abstraction;

namespace NOF.Hosting.AspNetCore;

/// <summary>
/// Request outbound middleware that populates outbound headers from the current HTTP request.
/// </summary>
public sealed class HttpHeaderOutboundMiddleware : IRequestOutboundMiddleware
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HttpHeaderOutboundOptions _options;

    public HttpHeaderOutboundMiddleware(
        IHttpContextAccessor httpContextAccessor,
        IOptions<HttpHeaderOutboundOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            foreach (var header in httpContext.Request.Headers)
            {
                if (IsAllowed(header.Key) && !context.Headers.ContainsKey(header.Key))
                {
                    context.Headers[header.Key] = header.Value.ToString();
                }
            }
        }

        return next(cancellationToken);
    }

    private bool IsAllowed(string headerName)
        => _options.AllowedHeaders.Any(pattern => headerName.MatchWildcard(pattern, StringComparison.OrdinalIgnoreCase));
}
