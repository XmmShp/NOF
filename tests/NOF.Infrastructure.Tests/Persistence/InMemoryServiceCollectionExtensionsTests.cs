using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Infrastructure;
using Xunit;

namespace NOF.Infrastructure.Tests.Persistence;

public sealed class InMemoryServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInMemoryPersistence_OnServices_ShouldRegisterInMemoryPersistenceServices()
    {
        var services = new ServiceCollection();

        services.AddInMemoryPersistence();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<InMemoryPersistenceStore>());
        Assert.Equal("InMemoryDbContext", scope.ServiceProvider.GetRequiredService<IDbContext>().GetType().Name);
    }

    [Fact]
    public void AddInMemoryPersistence_MultipleCalls_ShouldRemainIdempotent()
    {
        var services = new ServiceCollection();

        services.AddInMemoryPersistence();
        services.AddInMemoryPersistence();

        Assert.Single(services, static descriptor => descriptor.ServiceType == typeof(InMemoryPersistenceStore));
        Assert.Single(services, static descriptor => descriptor.ServiceType == typeof(IDbContext));
    }
}
