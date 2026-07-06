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

        services.AddNOFDomain(applicationId: 1, instanceId: 1);

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
    public void AddNOFDomain_ShouldResolveConfiguredSnowflakeGenerator()
    {
        var services = new ServiceCollection();
        services.AddNOFDomain(applicationId: 7, instanceId: 9);

        using var provider = services.BuildServiceProvider();
        var generator = Assert.IsType<SnowflakeIdGenerator>(provider.GetRequiredService<IIdGenerator>());
        var id = generator.NextId();

        const int sequenceBits = 8;
        const int instanceIdBits = 6;
        const int applicationIdBits = 8;
        var maxInstanceId = (1L << instanceIdBits) - 1;
        var maxApplicationId = (1L << applicationIdBits) - 1;
        var extractedInstanceId = (uint)((id >> sequenceBits) & maxInstanceId);
        var extractedApplicationId = (uint)((id >> (sequenceBits + instanceIdBits)) & maxApplicationId);

        Assert.Equal((uint)9, extractedInstanceId);
        Assert.Equal((uint)7, extractedApplicationId);
    }

    [Fact]
    public void AddNOFDomain_WithInvalidSnowflakeOptions_ShouldThrow()
    {
        var services = new ServiceCollection();

        void Act() => services.AddNOFDomain(
            applicationId: 0,
            instanceId: 1,
            configure: options => options.ApplicationIdBits = 0);

        Assert.Throws<ArgumentOutOfRangeException>(Act);
    }

    [Fact]
    public void AddNOFDomain_WithExplicitGenerator_ShouldRegisterProvidedInstance()
    {
        var services = new ServiceCollection();
        var generator = new TestIdGenerator();

        services.AddNOFDomain(generator);

        using var provider = services.BuildServiceProvider();
        Assert.Same(generator, provider.GetRequiredService<IIdGenerator>());
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public long NextId() => 42;
    }
}
