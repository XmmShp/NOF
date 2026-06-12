using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace NOF.Abstraction.Tests;

public class AutoInjectRegistryTests
{
    [Fact]
    public void GetOrAddSingleton_ShouldReuseExistingSingletonInstance()
    {
        var services = new ServiceCollection();
        var existing = new Foo();
        services.AddSingleton(existing);

        var resolved = AssemblyInitializationServices.GetOrAddSingleton<Foo>(services);

        Assert.Same(existing, resolved);
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(Foo));
    }

    [Fact]
    public void GetOrAddSingleton_ShouldCreateAndRegisterSingletonInstance()
    {
        var services = new ServiceCollection();

        var created = AssemblyInitializationServices.GetOrAddSingleton<Foo>(services);

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(Foo));
        Assert.Same(created, descriptor.ImplementationInstance);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void InitializedTypes_ShouldTrackInitializedTypesPerServiceCollection()
    {
        var services = new ServiceCollection();
        var state = services.InitializedTypes;

        var first = state.Add(typeof(Foo));
        var second = state.Add(typeof(Foo));

        Assert.True(first);
        Assert.False(second);
    }

    private sealed class Foo;
}
