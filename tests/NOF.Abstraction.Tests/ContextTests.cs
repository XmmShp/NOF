using NOF.Contract;
using Xunit;

namespace NOF.Abstraction.Tests;

public class ContextTests
{
    [Fact]
    public void StringItemKeys_ShouldBeCaseInsensitive()
    {
        var context = Context.Empty.WithItem("x-tenant-id", "tenant-a");

        Assert.True(context.TryGetItem("X-Tenant-Id", out var tenantId));
        Assert.Equal("tenant-a", tenantId);
        Assert.True(context.Items.ContainsKey("X-TENANT-ID"));
    }

    [Fact]
    public void WithItem_WithEquivalentStringKey_ShouldReplaceExistingItem()
    {
        var context = Context.Empty
            .WithItem("X-Tenant-Id", "tenant-a")
            .WithItem("x-tenant-id", "tenant-b");

        var item = Assert.Single(context.Items);
        Assert.Equal("tenant-b", item.Value);
    }

    [Fact]
    public void WithoutItem_WithEquivalentStringKey_ShouldRemoveExistingItem()
    {
        var context = Context.Empty
            .WithItem("x-tenant-id", "tenant-a")
            .WithoutItem("X-Tenant-Id");

        Assert.Empty(context.Items);
    }

    [Fact]
    public void NonStringItemKeys_ShouldKeepDefaultEqualitySemantics()
    {
        var firstKey = new object();
        var secondKey = new object();
        var context = Context.Empty
            .WithItem(firstKey, "first")
            .WithItem(secondKey, "second");

        Assert.Equal("first", context[firstKey]);
        Assert.Equal("second", context[secondKey]);
        Assert.Equal(2, context.Items.Count);
    }
}
