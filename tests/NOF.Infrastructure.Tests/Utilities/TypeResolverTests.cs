using Xunit;

namespace NOF.Infrastructure.Tests.Utilities;

public sealed class TypeResolverTests
{
    [Fact]
    public void Resolve_ShouldReturnRegisteredType()
    {
        var typeName = NOF.Abstraction.TypeResolver.Register(typeof(LoadedAssemblyPayload));

        var type = NOF.Abstraction.TypeResolver.Resolve(typeName);

        Assert.Equal(typeof(LoadedAssemblyPayload), type);
    }

    [Fact]
    public void ResolveFromLoadedAssemblies_ShouldFallbackToLoadedAssemblyTypeByFullName()
    {
        var type = NOF.Abstraction.TypeResolver.ResolveFromLoadedAssemblies(typeof(LoadedAssemblyPayload).FullName!);

        Assert.Equal(typeof(LoadedAssemblyPayload), type);
    }

    [Fact]
    public void Resolve_ShouldThrow_WhenTypeCannotBeFound()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => NOF.Abstraction.TypeResolver.Resolve("NOF.Tests.MissingPayload"));

        Assert.Contains("NOF.Tests.MissingPayload", exception.Message);
    }

    private sealed record LoadedAssemblyPayload(string Value);
}
