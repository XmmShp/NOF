using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Hosting.AspNetCore;
using Xunit;

[assembly: AssemblyInitialize<NOF.Hosting.AspNetCore.Tests.__TypeResolverSynchronizationAssemblyInitializer>]

namespace NOF.Hosting.AspNetCore.Tests;

public sealed class TypeResolverSynchronizationTests
{
    [Fact]
    public void Create_ShouldRunAssemblyInitializerThatRegistersGeneratedServicesIntoServiceCollection()
    {
        var builder = NOFWebApplicationBuilder.Create([]);

        Assert.Contains(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(__TypeResolverSynchronizationProbe)
                && descriptor.ImplementationType == typeof(__TypeResolverSynchronizationProbe));
    }
}

internal sealed class __TypeResolverSynchronizationAssemblyInitializer : IAssemblyInitializer
{
    public static void Initialize(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        services.AddSingleton<__TypeResolverSynchronizationProbe>();
    }
}

internal sealed class __TypeResolverSynchronizationProbe;
