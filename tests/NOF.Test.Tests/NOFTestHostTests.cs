using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
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
        scope.GetRequiredService<IExecutionContext>());
        Assert.NotNull(
        scope.GetRequiredService<IUserContext>());
    }

    [Fact]
    public async Task Scope_ShouldAllowSettingTenantAndUser()
    {
        var builder = NOFTestAppBuilder.Create();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        scope.SetTenant("tenant-a")
            .SetTracing("trace-1", "span-1")
            .SetUser("user-1", "Alice", ["orders.read", "orders.write"]);
        Assert.Equal("tenant-a",

        scope.ExecutionContext.TenantId);
        Assert.NotNull(
        scope.ExecutionContext.TracingInfo);
        Assert.Equal("trace-1",
        scope.ExecutionContext.TracingInfo!.TraceId);
        Assert.Equal("span-1",
        scope.ExecutionContext.TracingInfo!.SpanId);
        Assert.Equal("user-1",
        scope.UserContext.User.Id);
        Assert.Equal("Alice",
        scope.UserContext.User.Name);
        Assert.Contains("orders.read", scope.UserContext.User.Permissions);
        Assert.Contains("orders.write", scope.UserContext.User.Permissions);
    }

    [Fact]
    public async Task SendAsync_Command_ShouldResolveCommandSenderFromScope()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.Services.AddScoped<ICommandSender, FakeCommandSender>();

        await using var host = await builder.BuildTestHostAsync();
        Func<Task> act = () => host.SendAsync(new TestCommand("do-it"));

        await Record.ExceptionAsync(act);
    }

    [Fact]
    public async Task PublishAsync_ShouldResolveNotificationPublisherFromScope()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.Services.AddScoped<INotificationPublisher, FakeNotificationPublisher>();

        await using var host = await builder.BuildTestHostAsync();
        Func<Task> act = () => host.PublishAsync(new TestNotification("evt"));

        await Record.ExceptionAsync(act);
    }

    [Fact]
    public async Task GetRequiredService_ShouldInitializeSingletonOnlyOnce()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.Services.AddSingleton<SingletonInitializable>();

        await using var host = await builder.BuildTestHostAsync();

        var first = host.GetRequiredService<SingletonInitializable>();
        var second = host.GetRequiredService<SingletonInitializable>();

        Assert.Same(first, second);
        Assert.Equal(1,
        first.InitializeCount);
    }

    [Fact]
    public async Task GetRequiredService_ShouldInitializeScopedServiceOncePerScope()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.Services.AddScoped<ScopedInitializable>();

        await using var host = await builder.BuildTestHostAsync();

        using var firstScope = host.CreateScope();
        var firstA = firstScope.GetRequiredService<ScopedInitializable>();
        var firstB = firstScope.GetRequiredService<ScopedInitializable>();

        using var secondScope = host.CreateScope();
        var second = secondScope.GetRequiredService<ScopedInitializable>();

        Assert.Same(firstA, firstB);
        Assert.Equal(1,
        firstA.InitializeCount);
        Assert.Equal(1,
        second.InitializeCount);
        Assert.NotEqual(firstA.ScopeInstanceId,
        second.ScopeInstanceId);
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
        Assert.False(
        lazy.IsValueCreated);
        Assert.Equal(0,
        LazyProbe.CreatedCount);

        var probe = lazy.Value;
        Assert.NotNull(

        probe);
        Assert.True(
        lazy.IsValueCreated);
        Assert.Equal(1,
        LazyProbe.CreatedCount);
    }

    private sealed record TestCommand(string Value) : ICommand;

    private sealed record TestNotification(string Value) : INotification;

    private sealed class FakeCommandSender : ICommandSender
    {
        public void DeferSend(ICommand command)
        {
        }

        public Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotificationPublisher : INotificationPublisher
    {
        public void DeferPublish(INotification notification)
        {
        }

        public Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class SingletonInitializable : IInitializable
    {
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;

        public int InitializeCount { get; private set; }

        public void Initialize()
        {
            InitializeCount++;
            _isInitialized = true;
        }
    }

    private sealed class ScopedInitializable : IInitializable
    {
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;

        public Guid ScopeInstanceId { get; } = Guid.NewGuid();

        public int InitializeCount { get; private set; }

        public void Initialize()
        {
            InitializeCount++;
            _isInitialized = true;
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
