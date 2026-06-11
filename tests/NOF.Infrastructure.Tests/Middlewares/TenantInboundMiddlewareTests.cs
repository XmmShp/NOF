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
        var message = new object();
        var inboundContext = CreateContext(message.GetType());
        var forwardedContext = Context.Empty;

        await middleware.InvokeAsync(inboundContext, message, CaptureNextContext, default);
        Assert.Equal(NOFAbstractionConstants.Tenant.HostId, forwardedContext.TenantId);

        ValueTask CaptureNextContext(CommandInboundContext context, object forwardedMessage, CancellationToken cancellationToken)
        {
            _ = forwardedMessage;
            _ = cancellationToken;
            forwardedContext = context;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task InvokeAsync_WithIncomingTenantHeader_ShouldUseIncomingTenantHeader()
    {
        var middleware = new TenantInboundMiddleware();
        var message = new object();
        var inboundContext = (CommandInboundContext)CreateContext(message.GetType())
            .CopyHeadersFrom([
                new KeyValuePair<string, string?>(NOFAbstractionConstants.Transport.Headers.TenantId, "tenanta")
            ]);
        var forwardedContext = Context.Empty;

        await middleware.InvokeAsync(inboundContext, message, CaptureNextContext, default);
        Assert.Equal("tenanta", forwardedContext.TenantId);

        ValueTask CaptureNextContext(CommandInboundContext context, object forwardedMessage, CancellationToken cancellationToken)
        {
            _ = forwardedMessage;
            _ = cancellationToken;
            forwardedContext = context;
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
