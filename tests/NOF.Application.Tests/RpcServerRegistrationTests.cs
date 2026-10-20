using Microsoft.Extensions.DependencyInjection;
using NOF.Contract;
using Xunit;

namespace NOF.Application.Tests;

public class RpcServerRegistrationTests
{
    [Fact]
    public void RegistrationIsIdempotentAndServerLifetimeIsScoped()
    {
        var services = new ServiceCollection();
        Assert.Same(services, services.AddRpcServer<FirstServer>());
        services.AddRpcServer<FirstServer>();
        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(FirstServer));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Single(services, item => item.ServiceType == typeof(RpcServerRegistry));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var registry = provider.GetRequiredService<RpcServerRegistry>();
        Assert.True(registry.TryGetImplementationType(typeof(IRegistrationService), out var implementation));
        Assert.Equal(typeof(FirstServer), implementation);
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        Assert.Same(first.ServiceProvider.GetRequiredService<FirstServer>(), first.ServiceProvider.GetRequiredService<FirstServer>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<FirstServer>(), second.ServiceProvider.GetRequiredService<FirstServer>());
    }

    [Fact]
    public void LastRegistrationWinsWithoutChangingOtherServiceCollections()
    {
        var first = new ServiceCollection();
        var second = new ServiceCollection();
        first.AddRpcServer<FirstServer>();
        first.AddRpcServer<ReplacementServer>();
        second.AddRpcServer<FirstServer>();
        using var firstProvider = first.BuildServiceProvider();
        using var secondProvider = second.BuildServiceProvider();
        Assert.True(firstProvider.GetRequiredService<RpcServerRegistry>().TryGetImplementationType(typeof(IRegistrationService), out var replacement));
        Assert.Equal(typeof(ReplacementServer), replacement);
        Assert.True(secondProvider.GetRequiredService<RpcServerRegistry>().TryGetImplementationType(typeof(IRegistrationService), out var original));
        Assert.Equal(typeof(FirstServer), original);
    }

    [Fact]
    public void ApplicationDefaultsPreserveAnExistingServerRegistration()
    {
        var services = new ServiceCollection();
        services.AddRpcServer<FirstServer>();
        services.AddNOFApplication();
        using var provider = services.BuildServiceProvider();
        Assert.True(provider.GetRequiredService<RpcServerRegistry>().TryGetImplementationType(typeof(IRegistrationService), out var implementation));
        Assert.Equal(typeof(FirstServer), implementation);
    }

    public interface IRegistrationService : IRpcService;

    public class FirstServer : RpcServer<IRegistrationService>, IRpcServer
    {
        public static IReadOnlyDictionary<string, RpcHandlerMapping> HandlerMappings { get; } = new Dictionary<string, RpcHandlerMapping>();
        protected override IReadOnlyDictionary<string, RpcHandlerMapping> GetHandlerMappings() => HandlerMappings;
    }

    public class ReplacementServer : RpcServer<IRegistrationService>, IRpcServer
    {
        public static IReadOnlyDictionary<string, RpcHandlerMapping> HandlerMappings { get; } = new Dictionary<string, RpcHandlerMapping>();
        protected override IReadOnlyDictionary<string, RpcHandlerMapping> GetHandlerMappings() => HandlerMappings;
    }
}
