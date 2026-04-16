using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Application;
using Xunit;
using ExecutionContext = NOF.Application.ExecutionContext;

namespace NOF.Infrastructure.Tests.Middlewares;

public class TenantInboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SingleTenantMode_ShouldIgnoreIncomingTenantHeader()
    {
        var executionContext = new ExecutionContext
        {
            [NOFAbstractionConstants.Transport.Headers.TenantId] = "tenant-a"
        };
        var middleware = new TenantInboundMiddleware(
            executionContext,
            Options.Create(new TenantOptions
            {
                Mode = TenantMode.SingleTenant,
                SingleTenantId = "host"
            }));

        await middleware.InvokeAsync(CreateContext(), _ => ValueTask.CompletedTask, default);
        Assert.Equal("host",

        executionContext.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_SharedDatabaseMode_ShouldUseIncomingTenantHeader()
    {
        var executionContext = new ExecutionContext
        {
            [NOFAbstractionConstants.Transport.Headers.TenantId] = "tenant-a"
        };
        var middleware = new TenantInboundMiddleware(
            executionContext,
            Options.Create(new TenantOptions
            {
                Mode = TenantMode.SharedDatabase
            }));

        await middleware.InvokeAsync(CreateContext(), _ => ValueTask.CompletedTask, default);
        Assert.Equal("tenant-a", executionContext.TenantId);
    }

    private static CommandInboundContext CreateContext()
    {
        return new CommandInboundContext
        {
            Message = new object(),
            Services = new ServiceCollection().BuildServiceProvider(),
            Attributes = new List<Attribute>(),
            HandlerType = typeof(object)
        };
    }
}
