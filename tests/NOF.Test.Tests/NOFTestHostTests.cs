using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using System.Diagnostics;
using Xunit;

namespace NOF.Test.Tests;

public class NOFTestHostTests
{
    [Fact]
    public async Task BuildTestHostAsync_ShouldBuildHostWithDefaultContexts()
    {
        var builder = NOFTestAppBuilder.Create();

        await using var host = await builder.BuildTestHostAsync();

        using var scope = host.CreateScope();
        Assert.NotNull(
        scope.GetRequiredService<IContextAccessor>().Context);
        Assert.NotNull(
        scope.GetRequiredService<IUserContext>());
        Assert.Same(
            scope.GetRequiredService<IContextAccessor>().Context,
            scope.GetRequiredService<IContextAccessor>().Context);
    }

    [Fact]
    public async Task Scope_ShouldAllowSettingTenantAndUser()
    {
        var builder = NOFTestAppBuilder.Create();
        const string traceId = "0123456789abcdef0123456789abcdef";
        const string spanId = "0123456789abcdef";

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        scope.SetTenant("tenanta")
            .SetTracing(traceId, spanId)
            .SetUser("user-1", "Alice", ["orders.read", "orders.write"]);
        Assert.Equal("tenanta", scope.Context.TenantId);
        Assert.NotNull(Activity.Current);
        Assert.Equal(traceId, Activity.Current.TraceId.ToString());
        Assert.Equal(spanId, Activity.Current.ParentSpanId.ToString());
        Assert.Equal("user-1", scope.UserContext.User.Id);
        Assert.Equal("Alice", scope.UserContext.User.Name);
        Assert.Contains("orders.read", scope.UserContext.User.Permissions);
        Assert.Contains("orders.write", scope.UserContext.User.Permissions);
    }

    [Fact]
    public async Task SendAsync_Command_ShouldResolveCommandSenderFromScope()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.Services.AddScoped<ICommandSender, FakeCommandSender>();

        await using var host = await builder.BuildTestHostAsync();
        Func<Task> act = () => host.SendAsync(new TestCommand("do-it"), Context.Empty);

        await Record.ExceptionAsync(act);
    }

    [Fact]
    public async Task PublishAsync_ShouldResolveNotificationPublisherFromScope()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.Services.AddScoped<INotificationPublisher, FakeNotificationPublisher>();

        await using var host = await builder.BuildTestHostAsync();
        Func<Task> act = () => host.PublishAsync(new TestNotification("evt"), Context.Empty);

        await Record.ExceptionAsync(act);
    }

    [Fact]
    public async Task CreateScope_ShouldResolveLazyServiceFromHostingDefaults()
    {
        LazyProbe.Reset();
        var builder = NOFTestAppBuilder.Create();
        builder.Services.AddScoped<LazyProbe>();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        var lazy = scope.GetRequiredService<Lazy<LazyProbe>>();
        Assert.False(lazy.IsValueCreated);
        Assert.Equal(0, LazyProbe.CreatedCount);

        var probe = lazy.Value;
        Assert.NotNull(probe);
        Assert.True(lazy.IsValueCreated);
        Assert.Equal(1, LazyProbe.CreatedCount);
    }

    private sealed record TestCommand(string Value);

    private sealed record TestNotification(string Value);

    private sealed class FakeCommandSender : ICommandSender
    {
        public Task DeferSend(object command, Type commandType, Context context, CancellationToken cancellationToken = default)
        {
            _ = context;
            return Task.CompletedTask;
        }

        public Task SendAsync(object command, Type commandType, Context context, CancellationToken cancellationToken = default)
        {
            _ = context;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotificationPublisher : INotificationPublisher
    {
        public Task DeferPublish(object notification, Type[] notificationTypes, Context context, CancellationToken cancellationToken = default)
        {
            _ = context;
            return Task.CompletedTask;
        }

        public Task PublishAsync(object notification, Type[] notificationTypes, Context context, CancellationToken cancellationToken = default)
        {
            _ = context;
            return Task.CompletedTask;
        }
    }

    private sealed class LazyProbe
    {
        private static int _createdCount;

        public LazyProbe()
        {
            Interlocked.Increment(ref _createdCount);
        }

        public static int CreatedCount => _createdCount;

        public static void Reset()
        {
            Interlocked.Exchange(ref _createdCount, 0);
        }
    }
}
