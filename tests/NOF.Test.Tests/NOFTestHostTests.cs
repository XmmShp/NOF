using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
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
        scope.GetRequiredService<IMutableInvocationContext>().Should().NotBeNull();
        scope.GetRequiredService<IMutableUserContext>().Should().NotBeNull();
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

        scope.InvocationContext.TenantId.Should().Be("tenant-a");
        scope.InvocationContext.TraceId.Should().Be("trace-1");
        scope.InvocationContext.SpanId.Should().Be("span-1");
        scope.UserContext.Id.Should().Be("user-1");
        scope.UserContext.Name.Should().Be("Alice");
        scope.UserContext.Permissions.Should().Contain(["orders.read", "orders.write"]);
    }

    [Fact]
    public async Task SendAsync_ShouldResolveRequestSenderFromScope()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.Services.AddScoped<IRequestSender, FakeRequestSender>();

        await using var host = await builder.BuildTestHostAsync();
        var result = await host.SendAsync(new PingRequest("hello"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_Generic_ShouldResolveTypedResponse()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.Services.AddScoped<IRequestSender, FakeRequestSender>();

        await using var host = await builder.BuildTestHostAsync();
        var result = await host.SendAsync(new EchoRequest("hello"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public async Task SendAsync_Command_ShouldResolveCommandSenderFromScope()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.Services.AddScoped<ICommandSender, FakeCommandSender>();

        await using var host = await builder.BuildTestHostAsync();
        Func<Task> act = () => host.SendAsync(new TestCommand("do-it"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_ShouldResolveNotificationPublisherFromScope()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.Services.AddScoped<INotificationPublisher, FakeNotificationPublisher>();

        await using var host = await builder.BuildTestHostAsync();
        Func<Task> act = () => host.PublishAsync(new TestNotification("evt"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetRequiredService_ShouldInitializeSingletonOnlyOnce()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.Services.AddSingleton<SingletonInitializable>();

        await using var host = await builder.BuildTestHostAsync();

        var first = host.GetRequiredService<SingletonInitializable>();
        var second = host.GetRequiredService<SingletonInitializable>();

        first.Should().BeSameAs(second);
        first.InitializeCount.Should().Be(1);
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

        firstA.Should().BeSameAs(firstB);
        firstA.InitializeCount.Should().Be(1);
        second.InitializeCount.Should().Be(1);
        second.ScopeInstanceId.Should().NotBe(firstA.ScopeInstanceId);
    }

    private sealed record PingRequest(string Value) : IRequest;

    private sealed record EchoRequest(string Value) : IRequest<string>;

    private sealed record TestCommand(string Value) : ICommand;

    private sealed record TestNotification(string Value) : INotification;

    private sealed class FakeRequestSender : IRequestSender
    {
        public Task<Result> SendAsync(IRequest request, IDictionary<string, string?>? headers, string? destinationEndpointName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }

        public Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, IDictionary<string, string?>? headers, string? destinationEndpointName, CancellationToken cancellationToken = default)
        {
            object? value = request switch
            {
                EchoRequest echo when typeof(TResponse) == typeof(string) => echo.Value,
                _ => default(TResponse)
            };

            return Task.FromResult(Result.Success((TResponse)value!));
        }
    }

    private sealed class FakeCommandSender : ICommandSender
    {
        public Task SendAsync(ICommand command, IDictionary<string, string?>? headers, string? destinationEndpointName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotificationPublisher : INotificationPublisher
    {
        public Task PublishAsync(INotification notification, IDictionary<string, string?>? headers, CancellationToken cancellationToken = default)
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
}
