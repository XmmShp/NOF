using Microsoft.AspNetCore.Http;
using NOF.Application;
using NOF.Infrastructure.Core;
using System.Security.Claims;

namespace NOF.Hosting.AspNetCore;

/// <summary>
/// Authentication context middleware that extracts user and tenant information from claims and sets them on the InvocationContext.
/// </summary>
public class InvocationContextMiddleware : IMiddleware
{
    private readonly IInvocationContextInternal _invocationContext;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="invocationContext">The invocation context.</param>
    public InvocationContextMiddleware(IInvocationContextInternal invocationContext)
    {
        _invocationContext = invocationContext;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // Set user context
            await _invocationContext.SetUserAsync(context.User);

            // Extract tenant information
            var tenantId = context.User.FindFirstValue(NOFConstants.TenantId);

            // Set tenant context
            _invocationContext.SetTenantId(tenantId);
        }
        else
        {
            // Clear context when not authenticated
            await _invocationContext.UnsetUserAsync();
            _invocationContext.SetTenantId(null);
        }

        await next(context);
    }
}
