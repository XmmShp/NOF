using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace NOF.Hosting.AspNetCore;

/// <summary>
/// Activates scoped daemon services for every ASP.NET Core request so ambient infrastructure
/// such as mapper, event publisher, and context accessors are available consistently.
/// </summary>
public sealed class DaemonServiceResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public DaemonServiceResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.RequestServices.ResolveDaemonServices();
        return _next(context);
    }
}
