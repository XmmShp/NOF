using Xunit;

namespace NOF.Infrastructure.Tests.Utilities;

public sealed class TypeResolverTests
{
    [Fact]
    public void Resolve_ShouldReturnRegisteredType()
    {
        var resolver = new TypeResolver();
        var typeName = resolver.Register(typeof(LoadedAssemblyPayload));

        var type = resolver.Resolve(typeName);

        Assert.Equal(typeof(LoadedAssemblyPayload), type);
    }

    [Fact]
    public void ResolveFromLoadedAssemblies_ShouldFallbackToLoadedAssemblyTypeByFullName()
    {
        var resolver = new TypeResolver();

        var type = resolver.ResolveFromLoadedAssemblies(typeof(LoadedAssemblyPayload).FullName!);

        Assert.Equal(typeof(LoadedAssemblyPayload), type);
    }

    [Fact]
    public void Resolve_ShouldThrow_WhenTypeCannotBeFound()
    {
        var resolver = new TypeResolver();

        var exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("NOF.Tests.MissingPayload"));

        Assert.Contains("NOF.Tests.MissingPayload", exception.Message);
    }

    private sealed record LoadedAssemblyPayload(string Value);
}
