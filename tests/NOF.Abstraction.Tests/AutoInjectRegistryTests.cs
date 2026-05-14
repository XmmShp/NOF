using NOF.Annotation;
using Xunit;

namespace NOF.Abstraction.Tests;

public class AutoInjectRegistryTests
{
    [Fact]
    public void RegistryStorage_ShouldExposeSameAutoInjectRegistryInstance()
    {
        var registry = new Registry();
        registry.AutoInjectRegistry.Add(new AutoInjectServiceRegistration(
            typeof(IFoo),
            typeof(Foo),
            Lifetime.Scoped,
            UseFactory: false));

        Assert.Contains(registry.AutoInjectRegistry.Freeze(), r =>
            r.ServiceType == typeof(IFoo) &&
            r.ImplementationType == typeof(Foo));
    }

    [Fact]
    public void Registrations_FirstReadShouldFreezeRegistry()
    {
        var registry = new AutoInjectRegistry();
        registry.Add(new AutoInjectServiceRegistration(typeof(IFoo), typeof(Foo), Lifetime.Scoped, UseFactory: false));

        _ = registry.Freeze();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Add(new AutoInjectServiceRegistration(typeof(IFoo), typeof(Foo), Lifetime.Singleton, UseFactory: false)));
    }

    [Fact]
    public void RemoveAndRemoveWhere_ShouldRemoveMatchingRegistrations()
    {
        var first = new AutoInjectServiceRegistration(typeof(IFoo), typeof(Foo), Lifetime.Scoped, UseFactory: false);
        var second = new AutoInjectServiceRegistration(typeof(IFoo), typeof(Foo), Lifetime.Singleton, UseFactory: false);
        var registry = new AutoInjectRegistry();
        registry.Add(first);
        registry.Add(second);

        var removed = registry.Remove(first);
        var removedCount = registry.RemoveWhere(static registration => registration.Lifetime == Lifetime.Singleton);

        Assert.True(removed);
        Assert.Equal(1, removedCount);
        Assert.Empty(registry.Freeze());
    }

    private interface IFoo;
    private sealed class Foo : IFoo;
}
