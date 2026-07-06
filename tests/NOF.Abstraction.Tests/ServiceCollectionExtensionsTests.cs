using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace NOF.Abstraction.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNOFAbstraction_ShouldRegisterPackageDefaults()
    {
        var services = new ServiceCollection();

        services.AddNOFAbstraction();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(EventHandlerRegistry)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IUserContext)
            && descriptor.ImplementationType == typeof(UserContext)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IEventPublisher)
            && descriptor.ImplementationType == typeof(InMemoryEventPublisher)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IDaemonService)
            && descriptor.ImplementationType == typeof(EventPublisherAmbientDaemonService)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddNOFAbstraction_ShouldBeIdempotent()
    {
        var services = new ServiceCollection();

        services.AddNOFAbstraction();
        services.AddNOFAbstraction();

        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(EventHandlerRegistry));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IUserContext));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IEventPublisher));
        _ = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IDaemonService)
            && descriptor.ImplementationType == typeof(EventPublisherAmbientDaemonService));
    }
}
