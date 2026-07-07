using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using System.Diagnostics;
using System.Security.Claims;
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
        Assert.NotNull(scope.GetRequiredService<IUserContext>());
        Assert.Equal(NOFAbstractionConstants.Tenant.HostId, scope.GetRequiredService<ICurrentTenant>().TenantId);
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
        Assert.Equal("tenanta", scope.GetRequiredService<ICurrentTenant>().TenantId);
        Assert.NotNull(Activity.Current);
        Assert.Equal(traceId, Activity.Current.TraceId.ToString());
        Assert.Equal(spanId, Activity.Current.ParentSpanId.ToString());
        Assert.Equal("user-1", scope.UserContext.User.Id);
        Assert.Equal("Alice", scope.UserContext.User.Name);
        Assert.Contains("orders.read", scope.UserContext.User.Permissions);
        Assert.Contains("orders.write", scope.UserContext.User.Permissions);
    }

    [Fact]
    public async Task Scope_ShouldAllowSettingContextItems()
    {
        var builder = NOFTestAppBuilder.Create();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        scope.SetContextItem("case", "scope")
            .SetContextItems(new Dictionary<object, object?> { ["step"] = 2 })
            .RemoveContextItem("step");

        Assert.Equal("scope", scope.Context["case"]);
        Assert.False(scope.Context.TryGetItem("step", out _));
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
    public async Task ExecuteAsync_ShouldCreateScopeAndRunScenario()
    {
        var builder = NOFTestAppBuilder.Create();

        await using var host = await builder.BuildTestHostAsync();
        var result = await host.ExecuteAsync(scope =>
        {
            scope.SetTenant("tenant-b")
                .SetUser("user-2")
                .SetContextItem("flow", "execute");

            return Task.FromResult(scope.Context["flow"]);
        });

        Assert.Equal("execute", result);
    }

    [Fact]
    public async Task CallAsync_ShouldUseScopeContextAndConfiguredClient()
    {
        var builder = NOFTestAppBuilder.Create()
            .AddLocalRpcClient<ITestClient, TestClient>();

        await using var host = await builder.BuildTestHostAsync();
        var result = await host.CallAsync<ITestClient, string>(
            (client, context, cancellationToken) => client.EchoAsync(context, cancellationToken),
            configure: scope => scope.SetContextItem("case", "host-call"));

        Assert.Equal("host-call", result);
    }

    [Fact]
    public async Task AddInMemoryPersistence_ShouldAllowPersistingAcrossScopes()
    {
        var builder = NOFTestAppBuilder.Create()
            .AddInMemoryPersistence();

        await using var host = await builder.BuildTestHostAsync();

        await host.ExecuteAsync(async scope =>
        {
            var dbContext = scope.GetRequiredService<IDbContext>();
            dbContext.Set<TestEntity>().Add(new TestEntity { Name = "persisted" });
            await dbContext.SaveChangesAsync();
        });

        var entity = await host.ExecuteAsync(async scope =>
        {
            return await scope.GetRequiredService<IDbContext>()
                .Set<TestEntity>()
                .FirstOrDefaultAsync(item => item.Name == "persisted");
        });

        Assert.NotNull(entity);
        Assert.Equal("persisted", entity.Name);
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

    private interface ITestClient
    {
        Task<string> EchoAsync(Context context, CancellationToken cancellationToken);
    }

    private sealed class TestClient : ITestClient
    {
        public Task<string> EchoAsync(Context context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult((string?)context["case"] ?? string.Empty);
        }
    }

    private sealed class TestEntity
    {
        public required string Name { get; set; }
    }

    private sealed class FakeCommandSender : ICommandSender
    {
        public Task DeferSendAsync(object command, Type commandType, Context context, CancellationToken cancellationToken = default)
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
        public Task DeferPublishAsync(object notification, Type[] notificationTypes, Context context, CancellationToken cancellationToken = default)
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
