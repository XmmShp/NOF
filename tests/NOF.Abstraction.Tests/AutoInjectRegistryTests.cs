using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace NOF.Abstraction.Tests;

public class AutoInjectRegistryTests
{
    [Fact]
    public void RegistryStorage_ShouldExposeSameAutoInjectRegistryInstance()
    {
        var registry = new Registry();
        registry.AutoInjectRegistry.Add(ServiceDescriptor.Scoped<IFoo, Foo>());

        Assert.Contains(registry.AutoInjectRegistry.Freeze(), r =>
            r.ServiceType == typeof(IFoo) &&
            r.ImplementationType == typeof(Foo));
    }

    [Fact]
    public void RegistryCtor_ShouldAutoRegisterAutoInjectRegistryAsSameSingletonInstance()
    {
        var registry = new Registry();

        var registration = Assert.Single(
            registry.AutoInjectRegistry.Freeze(),
            r => r.ServiceType == typeof(AutoInjectRegistry));

        Assert.Same(registry.AutoInjectRegistry, registration.ImplementationInstance);
        Assert.Null(registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
    }

    [Fact]
    public void GetOrAdd_ShouldAutoRegisterCreatedRegistryAsSameSingletonInstance()
    {
        var registry = new Registry();

        var eventHandlerRegistry = registry.EventHandlerRegistry;
        var registration = Assert.Single(
            registry.AutoInjectRegistry.Freeze(),
            r => r.ServiceType == typeof(EventHandlerRegistry));

        Assert.Same(eventHandlerRegistry, registration.ImplementationInstance);
        Assert.Null(registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
    }

    [Fact]
    public void Registrations_FirstReadShouldFreezeRegistry()
    {
        var registry = new AutoInjectRegistry();
        registry.Add(ServiceDescriptor.Scoped<IFoo, Foo>());

        _ = registry.Freeze();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Add(ServiceDescriptor.Singleton<IFoo, Foo>()));
    }

    [Fact]
    public void RemoveAndRemoveWhere_ShouldRemoveMatchingRegistrations()
    {
        var first = ServiceDescriptor.Scoped<IFoo, Foo>();
        var second = ServiceDescriptor.Singleton<IFoo, Foo>();
        var registry = new AutoInjectRegistry();
        registry.Add(first);
        registry.Add(second);

        var removed = registry.Remove(first);
        var removedCount = registry.RemoveWhere(static registration => registration.Lifetime == ServiceLifetime.Singleton);

        Assert.True(removed);
        Assert.Equal(1, removedCount);
        Assert.Empty(registry.Freeze());
    }

    private interface IFoo;
    private sealed class Foo : IFoo;
}
