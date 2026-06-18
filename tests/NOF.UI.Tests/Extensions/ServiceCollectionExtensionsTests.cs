using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;

namespace NOF.UI.Tests.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNOFUI_ShouldRegisterBrowserInfoService()
    {
        var services = new ServiceCollection();

        services.AddNOFUI();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBrowserInfoService) &&
            descriptor.ImplementationType == typeof(BrowserInfoService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void IBrowserInfoService_ShouldExposeChangedEvent()
    {
        var serviceType = typeof(IBrowserInfoService);

        Assert.NotNull(serviceType.GetEvent(nameof(IBrowserInfoService.Changed)));
        Assert.Null(serviceType.GetMethod("StartListeningAsync", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(serviceType.GetMethod("StopListeningAsync", BindingFlags.Public | BindingFlags.Instance));
    }
}
