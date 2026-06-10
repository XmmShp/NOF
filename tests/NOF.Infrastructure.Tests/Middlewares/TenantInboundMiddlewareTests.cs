using NOF.Abstraction;
using NOF.Application;
using Xunit;

namespace NOF.Infrastructure.Tests.Middlewares;

public class TenantInboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithoutIncomingTenantHeader_ShouldUseHostTenant()
    {
        var executionContext = new NOFContext();
        var middleware = new TenantInboundMiddleware(executionContext);

        await middleware.InvokeAsync(CreateContext(), _ => ValueTask.CompletedTask, default);
        Assert.Equal(NOFAbstractionConstants.Tenant.HostId, executionContext.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_WithIncomingTenantHeader_ShouldUseIncomingTenantHeader()
    {
        var executionContext = new NOFContext();
        executionContext.SetHeader(NOFAbstractionConstants.Transport.Headers.TenantId, "tenanta");
        var middleware = new TenantInboundMiddleware(executionContext);

        await middleware.InvokeAsync(CreateContext(), _ => ValueTask.CompletedTask, default);
        Assert.Equal("tenanta", executionContext.TenantId);
    }

    private static CommandInboundContext CreateContext()
    {
        return new CommandInboundContext
        {
            Message = new object(),
            HandlerType = typeof(object)
        };
    }
}
