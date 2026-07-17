using NOF.Application;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public sealed class MessageTypeResolverTests
{
    [Fact]
    public void Resolve_ShouldReturnCommandTypeFromRegistry()
    {
        var commandRegistry = new CommandHandlerRegistry();
        commandRegistry.Add(new CommandHandlerRegistration(typeof(TestCommandHandler), typeof(TestCommand)));
        var resolver = new MessageTypeResolver(commandRegistry, new NotificationHandlerRegistry());

        var result = resolver.Resolve(typeof(TestCommand).FullName!);

        Assert.Equal(typeof(TestCommand), result);
    }

    [Fact]
    public void Resolve_ShouldReturnNotificationTypeFromRegistry()
    {
        var notificationRegistry = new NotificationHandlerRegistry();
        notificationRegistry.Add(new NotificationHandlerRegistration(typeof(TestNotificationHandler), typeof(TestNotification)));
        var resolver = new MessageTypeResolver(new CommandHandlerRegistry(), notificationRegistry);

        var result = resolver.Resolve(typeof(TestNotification).FullName!);

        Assert.Equal(typeof(TestNotification), result);
    }

    [Fact]
    public void Resolve_ShouldThrow_WhenTypeIsNotRegistered()
    {
        var resolver = new MessageTypeResolver(new CommandHandlerRegistry(), new NotificationHandlerRegistry());

        var exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("App.MissingMessage"));

        Assert.Contains("App.MissingMessage", exception.Message);
    }

    private sealed record TestCommand(string Value);

    private sealed class TestCommandHandler;

    private sealed record TestNotification(string Value);

    private sealed class TestNotificationHandler;
}
