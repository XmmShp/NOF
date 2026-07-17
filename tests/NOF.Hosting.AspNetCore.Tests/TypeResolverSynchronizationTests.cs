using NOF.Abstraction;
using NOF.Hosting.AspNetCore;
using Xunit;

[assembly: AssemblyInitialize<NOF.Hosting.AspNetCore.Tests.__TypeResolverSynchronizationAssemblyInitializer>]

namespace NOF.Hosting.AspNetCore.Tests;

public sealed class TypeResolverSynchronizationTests
{
    [Fact]
    public void Create_ShouldRunAssemblyInitializerThatRegistersTypesIntoStaticTypeResolver()
    {
        _ = NOFWebApplicationBuilder.Create([]);

        Assert.Equal(typeof(__TypeResolverSynchronizationMessage), TypeResolver.Resolve(typeof(__TypeResolverSynchronizationMessage).DisplayName));
        Assert.Equal(typeof(__TypeResolverSynchronizationHandler), TypeResolver.ResolveHandler(typeof(__TypeResolverSynchronizationHandler).DisplayName));
    }
}

internal sealed class __TypeResolverSynchronizationAssemblyInitializer : IAssemblyInitializer
{
    public static void Initialize(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        TypeResolver.Register(typeof(__TypeResolverSynchronizationMessage));
        TypeResolver.Register(typeof(__TypeResolverSynchronizationHandler));
    }
}

internal sealed record __TypeResolverSynchronizationMessage(string Value);

internal sealed class __TypeResolverSynchronizationHandler;
