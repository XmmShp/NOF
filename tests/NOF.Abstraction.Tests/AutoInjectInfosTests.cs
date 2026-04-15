using NOF.Annotation;
using Xunit;

namespace NOF.Abstraction.Tests;

public class AutoInjectInfosTests
{
    [Fact]
    public void Registrations_FirstReadShouldImportStaticRegistryAndFreeze()
    {
        Registry.AutoInjectRegistrations.Add(new AutoInjectServiceRegistration(
            typeof(IFoo),
            typeof(Foo),
            Lifetime.Scoped,
            UseFactory: false));

        var infos = new AutoInjectInfos();

        Assert.Contains(infos.Registrations, r =>
            r.ServiceType == typeof(IFoo) &&
            r.ImplementationType == typeof(Foo));

        Assert.Throws<InvalidOperationException>(() =>
            infos.Add(new AutoInjectServiceRegistration(typeof(IBar), typeof(Bar), Lifetime.Transient, false)));
    }

    private interface IFoo;
    private interface IBar;
    private sealed class Foo : IFoo;
    private sealed class Bar : IBar;
}
