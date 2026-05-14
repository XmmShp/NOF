using NOF.Abstraction;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public sealed class TenantIdTests
{
    [Fact]
    public void Normalize_ShouldUseHostTenant_WhenTenantIdIsNullOrWhitespace()
    {
        Assert.Equal(NOFAbstractionConstants.Tenant.HostId, TenantId.Normalize(null));
        Assert.Equal(NOFAbstractionConstants.Tenant.HostId, TenantId.Normalize("   "));
    }

    [Fact]
    public void Normalize_ShouldTrimTenantId()
    {
        Assert.Equal("tenanta", TenantId.Normalize(" tenanta "));
    }
}
