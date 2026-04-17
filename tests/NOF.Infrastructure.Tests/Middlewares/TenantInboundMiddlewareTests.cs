using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;
using Xunit;
using ExecutionContext = NOF.Application.ExecutionContext;

namespace NOF.Infrastructure.Tests.Middlewares;

public class TenantInboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithoutIncomingTenantHeader_ShouldUseHostTenant()
    {
        var executionContext = new ExecutionContext();
        var middleware = new TenantInboundMiddleware(executionContext);

        await middleware.InvokeAsync(CreateContext(), _ => ValueTask.CompletedTask, default);
        Assert.Equal(NOFAbstractionConstants.Tenant.HostId, executionContext.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_WithIncomingTenantHeader_ShouldUseIncomingTenantHeader()
    {
        var executionContext = new ExecutionContext
        {
            [NOFAbstractionConstants.Transport.Headers.TenantId] = "tenanta"
        };
        var middleware = new TenantInboundMiddleware(executionContext);

        await middleware.InvokeAsync(CreateContext(), _ => ValueTask.CompletedTask, default);
        Assert.Equal("tenanta", executionContext.TenantId);
    }

    private static CommandInboundContext CreateContext()
    {
        return new CommandInboundContext
        {
            Message = new object(),
            Services = new ServiceCollection().BuildServiceProvider(),
            HandlerType = typeof(object)
        };
    }
}
