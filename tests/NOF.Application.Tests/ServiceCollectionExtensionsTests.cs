using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Domain;
using Xunit;

namespace NOF.Application.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNOFApplication_ShouldRegisterPackageDefaults()
    {
        var services = new ServiceCollection();

        services.AddNOFApplication();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(MapperRegistry) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(CommandHandlerRegistry) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(NotificationHandlerRegistry) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(RpcServerRegistry) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMapper) &&
            descriptor.ImplementationType == typeof(ManualMapper) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IStateMachineRegistry) &&
            descriptor.ImplementationType == typeof(StateMachineRegistry) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IIdGenerator) &&
            descriptor.ImplementationType == typeof(SnowflakeIdGenerator) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IDaemonService) &&
            descriptor.ImplementationType == typeof(MapperAmbientDaemonService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IDaemonService) &&
            descriptor.ImplementationType == typeof(IdGeneratorAmbientDaemonService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddNOFApplication_ShouldActivateAmbientMapperConvenienceApi()
    {
        var services = new ServiceCollection();
        services.AddNOFApplication();
        services.GetOrAddSingleton<MapperRegistry>()
            .Add(MapperRegistration.Of<int, string>(value => value.ToString()));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();

        Assert.Equal("42", 42.Map.To<string>());
    }

    [Fact]
    public void AddNOFApplication_ShouldBeIdempotent()
    {
        var services = new ServiceCollection();

        services.AddNOFApplication();
        services.AddNOFApplication();

        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(MapperRegistry));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(CommandHandlerRegistry));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(NotificationHandlerRegistry));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(RpcServerRegistry));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IMapper));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IStateMachineRegistry));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IIdGenerator));
        _ = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IDaemonService)
            && descriptor.ImplementationType == typeof(MapperAmbientDaemonService));
        _ = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IDaemonService)
            && descriptor.ImplementationType == typeof(IdGeneratorAmbientDaemonService));
    }

    [Fact]
    public void AddNOFApplication_ShouldAllowOverridingIIdGenerator()
    {
        var services = new ServiceCollection();
        var generator = new TestIdGenerator();

        services.AddNOFApplication();
        services.AddSingleton<IIdGenerator>(generator);

        using var provider = services.BuildServiceProvider();
        Assert.Same(generator, provider.GetRequiredService<IIdGenerator>());
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public long NextId() => 42;
    }
}
