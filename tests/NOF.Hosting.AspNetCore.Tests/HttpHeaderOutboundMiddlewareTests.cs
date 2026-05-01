using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NOF.Contract;
using Xunit;

namespace NOF.Hosting.AspNetCore.Tests;

public sealed class HttpHeaderOutboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenInboundRequestHasContentHeaders_DoesNotCopyThemToOutboundContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.ContentType = "application/json";
        httpContext.Request.Headers.ContentLength = 123;
        httpContext.Request.Headers.Authorization = "Bearer token";
        httpContext.Request.Headers["X-Tenant-Id"] = "tenant-1";

        var middleware = new HttpHeaderOutboundMiddleware(new HttpContextAccessor
        {
            HttpContext = httpContext
        }, Options.Create(new HttpHeaderOutboundOptions()));
        var context = CreateContext();

        await middleware.InvokeAsync(context, _ => ValueTask.CompletedTask, CancellationToken.None);

        Assert.False(context.Headers.ContainsKey("Content-Type"));
        Assert.False(context.Headers.ContainsKey("Content-Length"));
        Assert.Equal("Bearer token", context.Headers["Authorization"]);
        Assert.Equal("tenant-1", context.Headers["X-Tenant-Id"]);
    }

    [Fact]
    public async Task InvokeAsync_WhenOutboundContextAlreadyHasHeader_DoesNotOverwriteIt()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer inbound";

        var middleware = new HttpHeaderOutboundMiddleware(new HttpContextAccessor
        {
            HttpContext = httpContext
        }, Options.Create(new HttpHeaderOutboundOptions()));
        var context = CreateContext();
        context.Headers["Authorization"] = "Bearer existing";

        await middleware.InvokeAsync(context, _ => ValueTask.CompletedTask, CancellationToken.None);

        Assert.Equal("Bearer existing", context.Headers["Authorization"]);
    }

    [Fact]
    public async Task InvokeAsync_WhenAllowedHeadersContainsWildcard_CopiesMatchingHeaders()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-App-Trace"] = "trace-1";
        httpContext.Request.Headers["X-Other"] = "other";

        var options = new HttpHeaderOutboundOptions();
        options.AllowedHeaders.Clear();
        options.AllowedHeaders.Add("X-App-*");

        var middleware = new HttpHeaderOutboundMiddleware(new HttpContextAccessor
        {
            HttpContext = httpContext
        }, Options.Create(options));
        var context = CreateContext();

        await middleware.InvokeAsync(context, _ => ValueTask.CompletedTask, CancellationToken.None);

        Assert.Equal("trace-1", context.Headers["X-App-Trace"]);
        Assert.False(context.Headers.ContainsKey("X-Other"));
    }

    private static RequestOutboundContext CreateContext()
    {
        return new RequestOutboundContext
        {
            Message = new Empty(),
            ServiceType = typeof(HttpHeaderOutboundMiddlewareTests),
            MethodName = nameof(CreateContext)
        };
    }
}
