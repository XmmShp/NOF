using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using Xunit;

namespace NOF.Domain.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNOFDomain_ShouldRegisterDomainAndAbstractionDefaults()
    {
        var services = new ServiceCollection();

        services.AddNOFDomain();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IIdGenerator)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IDaemonService)
            && descriptor.ImplementationType == typeof(IdGeneratorAmbientDaemonService)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IEventPublisher)
            && descriptor.ImplementationType == typeof(InMemoryEventPublisher));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IUserContext)
            && descriptor.ImplementationType == typeof(UserContext));
    }

    [Fact]
    public void AddNOFDomain_ShouldResolveDefaultSnowflakeGenerator()
    {
        var services = new ServiceCollection();
        services.AddNOFDomain();

        using var provider = services.BuildServiceProvider();
        var generator = Assert.IsType<SnowflakeIdGenerator>(provider.GetRequiredService<IIdGenerator>());
        _ = generator.NextId();
    }

    [Fact]
    public void AddNOFDomain_ShouldAllowOverridingIIdGenerator()
    {
        var services = new ServiceCollection();
        var generator = new TestIdGenerator();

        services.AddNOFDomain();
        services.AddSingleton<IIdGenerator>(generator);

        using var provider = services.BuildServiceProvider();
        Assert.Same(generator, provider.GetRequiredService<IIdGenerator>());
    }

    [Fact]
    public void AddNOFDomain_ShouldBeIdempotent()
    {
        var services = new ServiceCollection();

        services.AddNOFDomain();
        services.AddNOFDomain();

        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IIdGenerator));
        _ = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IDaemonService)
            && descriptor.ImplementationType == typeof(IdGeneratorAmbientDaemonService));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IEventPublisher));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IUserContext));
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public long NextId() => 42;
    }
}
