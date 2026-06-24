using NOF.Contract;
using NOF.UI.Components;
using Xunit;

namespace NOF.UI.Tests.Components;

public sealed class PermissionRouteViewMetadataResolverTests
{
    [Fact]
    public void ResolveRequiredPermission_WithAllowAnonymousOverride_ReturnsNull()
    {
        var permission = PermissionRouteViewMetadataResolver.ResolveRequiredPermission(typeof(AnonymousPage));

        Assert.Null(permission);
    }

    [Fact]
    public void ResolveRequiredPermission_WithRequirePermission_ReturnsPermission()
    {
        var permission = PermissionRouteViewMetadataResolver.ResolveRequiredPermission(typeof(ProtectedPage));

        Assert.Equal("jobs.read", permission);
    }

    [RequirePermission("jobs.read")]
    private class ProtectedBasePage
    {
    }

    [AllowAnonymous]
    private sealed class AnonymousPage : ProtectedBasePage
    {
    }

    private sealed class ProtectedPage : ProtectedBasePage
    {
    }
}
