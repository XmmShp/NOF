using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
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

    [Fact]
    public void ReplaceOrAdd_ShouldReplaceExistingDescriptor()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IUserContext, UserContext>();
        services.ReplaceOrAdd(ServiceDescriptor.Scoped<IUserContext, TestUserContext>());

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IUserContext));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(TestUserContext), descriptor.ImplementationType);
    }

    [Fact]
    public void ReplaceOrAdd_ShouldAddDescriptorWhenMissing()
    {
        var services = new ServiceCollection();

        services.ReplaceOrAddSingleton<InitializedTypes, InitializedTypes>();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(InitializedTypes));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(InitializedTypes), descriptor.ImplementationType);
    }

    private sealed class TestUserContext : IUserContext
    {
        public event Action? StateChanging;

        public event Action? StateChanged;

        public ClaimsPrincipal User => new();

        public void Logout()
        {
            StateChanging?.Invoke();
            StateChanged?.Invoke();
        }
    }
}
