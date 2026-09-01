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
            descriptor.ServiceType == typeof(MappingRegistry) &&
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
            descriptor.ImplementationType == typeof(ExpressionMapper) &&
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
    public void AddNOFApplication_ShouldResolveExpressionMapper()
    {
        var services = new ServiceCollection();
        services.AddNOFApplication();
        services.GetOrAddSingleton<MappingRegistry>()
            .Add(MappingRegistration.Of<int, string>(value => value.ToString()));

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        Assert.Equal("42", mapper.Map<int, string>(42));
    }

    [Fact]
    public void AddNOFApplication_ShouldBindAmbientMapperForResolvedScope()
    {
        var services = new ServiceCollection();
        services.AddMapping<int, string>(value => "mapped:" + value);

        using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.ResolveDaemonServices();

            Assert.Same(scope.ServiceProvider.GetRequiredService<IMapper>(), Mapper.Current);
            Assert.Equal("mapped:5", new[] { 5 }.AsQueryable().ProjectTo<string>().Single());
        }

        Assert.Throws<InvalidOperationException>(() => Mapper.Current);
    }

    [Fact]
    public void AddMapping_ShouldRegisterExpressionAndApplicationDefaults()
    {
        var services = new ServiceCollection();
        services.AddMapping<int, string>(value => value.ToString());

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        Assert.Equal("7", mapper.Map<int, string>(7));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(MappingRegistry));
    }

    [Fact]
    public void AddNOFApplication_ShouldBeIdempotent()
    {
        var services = new ServiceCollection();

        services.AddNOFApplication();
        services.AddNOFApplication();

        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(MappingRegistry));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(CommandHandlerRegistry));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(NotificationHandlerRegistry));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(RpcServerRegistry));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IMapper));
        _ = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IIdGenerator));
        _ = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IDaemonService)
            && descriptor.ImplementationType == typeof(IdGeneratorAmbientDaemonService));
        _ = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IDaemonService)
            && descriptor.ImplementationType == typeof(MapperAmbientDaemonService));
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
