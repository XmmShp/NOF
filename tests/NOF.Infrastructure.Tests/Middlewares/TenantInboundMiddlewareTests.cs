using NOF.Abstraction;
using NOF.Contract;
using Xunit;

namespace NOF.Infrastructure.Tests.Middlewares;

public class TenantInboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithoutIncomingTenantHeader_ShouldUseHostTenant()
    {
        var currentTenant = new CurrentTenant();
        using var previousTenantScope = currentTenant.PushTenant("previous");
        var middleware = new TenantInboundMiddleware(currentTenant);
        var message = new object();
        var inboundContext = CreateContext(message.GetType());
        var tenantDuringNext = string.Empty;

        await middleware.InvokeAsync(inboundContext, message, CaptureNextContext, default);
        Assert.Equal(NOFAbstractionConstants.Tenant.HostId, tenantDuringNext);
        Assert.Equal("previous", currentTenant.TenantId);

        ValueTask CaptureNextContext(CommandInboundContext context, object forwardedMessage, CancellationToken cancellationToken)
        {
            _ = context;
            _ = forwardedMessage;
            _ = cancellationToken;
            tenantDuringNext = currentTenant.TenantId;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task InvokeAsync_WithIncomingTenantHeader_ShouldUseIncomingTenantHeader()
    {
        var currentTenant = new CurrentTenant();
        using var previousTenantScope = currentTenant.PushTenant("previous");
        var middleware = new TenantInboundMiddleware(currentTenant);
        var message = new object();
        var inboundContext = (CommandInboundContext)CreateContext(message.GetType())
            .CopyHeadersFrom([
                new KeyValuePair<string, string?>(NOFAbstractionConstants.Transport.Headers.TenantId, "tenanta")
            ]);
        var tenantDuringNext = string.Empty;

        await middleware.InvokeAsync(inboundContext, message, CaptureNextContext, default);
        Assert.Equal("tenanta", tenantDuringNext);
        Assert.Equal("previous", currentTenant.TenantId);

        ValueTask CaptureNextContext(CommandInboundContext context, object forwardedMessage, CancellationToken cancellationToken)
        {
            _ = context;
            _ = forwardedMessage;
            _ = cancellationToken;
            tenantDuringNext = currentTenant.TenantId;
            return ValueTask.CompletedTask;
        }
    }

    private static CommandInboundContext CreateContext(Type messageType)
    {
        return new CommandInboundContext
        {
            MethodInfo = typeof(object).GetMethod(nameof(ToString))!,
            HandlerType = typeof(object),
            MessageType = messageType
        };
    }
}
