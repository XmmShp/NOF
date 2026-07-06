using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace NOF.UI.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNOFUI_ShouldRegisterPackageDefaults()
    {
        var services = new ServiceCollection();

        services.AddNOFUI();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBrowserInfoService)
            && descriptor.ImplementationType == typeof(BrowserInfoService)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ILocalStorage)
            && descriptor.ImplementationType == typeof(LocalStorage)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISessionStorage)
            && descriptor.ImplementationType == typeof(SessionStorage)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddNOFUI_ShouldBeIdempotent()
    {
        var services = new ServiceCollection();

        services.AddNOFUI();
        services.AddNOFUI();

        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IBrowserInfoService));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ILocalStorage));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ISessionStorage));
    }
}
