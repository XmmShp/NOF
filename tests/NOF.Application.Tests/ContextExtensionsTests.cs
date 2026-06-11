using NOF.Contract;
using Xunit;

namespace NOF.Application.Tests;

public class ContextExtensionsTests
{
    [Fact]
    public void CopyHeadersFrom_ShouldStoreHeadersAsStringItems()
    {
        var context = Context.Empty.CopyHeadersFrom(
        [
            new KeyValuePair<string, string?>("X-Test", "value")
        ]);
        var item = Assert.Single(context.Items);

        Assert.Equal("X-Test", item.Key);
        Assert.Equal("value", item.Value);
    }

    [Fact]
    public void WithTenantId_ShouldSetTenantIdWithoutChangingItems()
    {
        var context = Context.Empty.WithTenantId("tenant-a");

        Assert.Equal("tenant-a", context.TenantId);
        Assert.Empty(context.Items);
    }

    [Fact]
    public void WithItem_ShouldNotAffectTenantId()
    {
        var context = Context.Empty.WithItem("NOF.TenantId", "tenant-a");

        Assert.Equal(string.Empty, context.TenantId);
    }
}
