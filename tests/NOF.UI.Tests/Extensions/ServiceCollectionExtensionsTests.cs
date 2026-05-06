using Microsoft.Extensions.DependencyInjection;
using NOF.UI;
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
}
