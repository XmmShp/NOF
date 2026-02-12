using Microsoft.AspNetCore.Http;
using NOF.Infrastructure.Core;

namespace NOF.Hosting.AspNetCore;

/// <summary>
/// ASP.NET Core implementation of <see cref="ITransportHeaderProvider"/> that reads
/// HTTP request headers from the current <see cref="HttpContext"/> via <see cref="IHttpContextAccessor"/>.
/// <para>
/// Registered as scoped so the handler pipeline can pull transport headers
/// without coupling to ASP.NET Core directly.
/// </para>
/// </summary>
public class HttpTransportHeaderProvider : ITransportHeaderProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTransportHeaderProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string?> GetHeaders()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return new Dictionary<string, string?>();
        }

        var headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in httpContext.Request.Headers)
        {
            headers[header.Key] = header.Value.ToString();
        }
        return headers;
    }
}
