using NOF.Abstraction;
using NOF.Contract;
using Xunit;

namespace NOF.Infrastructure.Tests.Middlewares;

public sealed class TenantHeaderOutboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithExplicitTenant_ShouldPreferInvocationContext()
    {
        using var _ = Context.PushCurrent(Context.Empty.WithTenantId("ambient"));
        var middleware = new TenantHeaderOutboundMiddleware();
        var context = new CommandOutboundContext(Context.Empty.WithTenantId("explicit"));

        await middleware.InvokeAsync(context, new object(), static (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal(
            "explicit",
            context.Headers[NOFAbstractionConstants.Transport.Headers.TenantId]);
    }

    [Fact]
    public async Task InvokeAsync_WithoutExplicitTenant_ShouldUseAmbientContext()
    {
        using var _ = Context.PushCurrent(Context.Empty.WithTenantId("ambient"));
        var middleware = new TenantHeaderOutboundMiddleware();
        var context = new CommandOutboundContext(Context.Empty);

        await middleware.InvokeAsync(context, new object(), static (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal(
            "ambient",
            context.Headers[NOFAbstractionConstants.Transport.Headers.TenantId]);
    }
}
