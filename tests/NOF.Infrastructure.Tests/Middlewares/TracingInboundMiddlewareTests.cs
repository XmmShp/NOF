using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NOF.Contract;
using Xunit;

namespace NOF.Infrastructure.Tests.Middlewares;

public sealed class TracingInboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_CommandException_ShouldPropagate()
    {
        var middleware = CreateMiddleware();
        var exception = new InvalidOperationException("Command failed.");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await middleware.InvokeAsync(
                CreateCommandContext(),
                new TestCommand(),
                (_, _, _) => throw exception,
                default));

        Assert.Same(exception, actual);
    }

    [Fact]
    public async Task InvokeAsync_NotificationException_ShouldPropagate()
    {
        var middleware = CreateMiddleware();
        var exception = new InvalidOperationException("Notification failed.");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await middleware.InvokeAsync(
                CreateNotificationContext(),
                new TestNotification(),
                (_, _, _) => throw exception,
                default));

        Assert.Same(exception, actual);
    }

    private static TracingInboundMiddleware CreateMiddleware()
        => new(
            Host.CreateApplicationBuilder().Environment,
            NullLogger<TracingInboundMiddleware>.Instance);

    private static CommandInboundContext CreateCommandContext()
        => new()
        {
            MethodInfo = typeof(TestHandler).GetMethod(nameof(TestHandler.HandleCommand))!,
            HandlerType = typeof(TestHandler),
            MessageType = typeof(TestCommand)
        };

    private static NotificationInboundContext CreateNotificationContext()
        => new()
        {
            MethodInfo = typeof(TestHandler).GetMethod(nameof(TestHandler.HandleNotification))!,
            HandlerType = typeof(TestHandler),
            MessageType = typeof(TestNotification)
        };

    private sealed class TestCommand;

    private sealed class TestNotification;

    private sealed class TestHandler
    {
        public void HandleCommand(TestCommand command) => _ = command;

        public void HandleNotification(TestNotification notification) => _ = notification;
    }
}
