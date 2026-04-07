using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using Xunit;

namespace NOF.Infrastructure.Tests.Middlewares;

public class TenantInboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SingleTenantMode_ShouldIgnoreIncomingTenantHeader()
    {
        var executionContext = new NOF.Contract.ExecutionContext
        {
            [NOFContractConstants.Transport.Headers.TenantId] = "tenant-a"
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
        var executionContext = new NOF.Contract.ExecutionContext
        {
            [NOFContractConstants.Transport.Headers.TenantId] = "tenant-a"
        };
        var middleware = new TenantInboundMiddleware(
            executionContext,
            Options.Create(new TenantOptions
            {
                Mode = TenantMode.SharedDatabase
            }));

        await middleware.InvokeAsync(CreateContext(), _ => ValueTask.CompletedTask, default);
        Assert.Equal("tenant-a",

        executionContext.TenantId);
    }

    private static InboundContext CreateContext()
    {
        return new InboundContext
        {
            Message = new object(),
            HandlerType = typeof(object),
            Services = new ServiceCollection().BuildServiceProvider()
        };
    }
}

