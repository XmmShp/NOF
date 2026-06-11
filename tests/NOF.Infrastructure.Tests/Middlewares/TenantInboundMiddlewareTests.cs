using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using Xunit;

namespace NOF.Infrastructure.Tests.Middlewares;

public class TenantInboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithoutIncomingTenantHeader_ShouldUseHostTenant()
    {
        var middleware = new TenantInboundMiddleware();
        var inboundContext = CreateContext();

        await middleware.InvokeAsync(inboundContext, _ => ValueTask.CompletedTask, default);
        Assert.Equal(NOFAbstractionConstants.Tenant.HostId, inboundContext.Context.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_WithIncomingTenantHeader_ShouldUseIncomingTenantHeader()
    {
        var middleware = new TenantInboundMiddleware();
        var inboundContext = CreateContext(Context.Empty.WithHeader(NOFAbstractionConstants.Transport.Headers.TenantId, "tenanta"));

        await middleware.InvokeAsync(inboundContext, _ => ValueTask.CompletedTask, default);
        Assert.Equal("tenanta", inboundContext.Context.TenantId);
    }

    private static CommandInboundContext CreateContext(Context? context = null)
    {
        return new CommandInboundContext
        {
            Context = context ?? Context.Empty,
            Message = new object(),
            HandlerType = typeof(object)
        };
    }
}
