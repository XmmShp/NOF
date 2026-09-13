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

    [Fact]
    public void TenantId_ShouldBeNormalizedAndDefaultToHost()
    {
        Assert.Equal("host", Context.Empty.TenantId);
        Assert.Equal("tenanta", Context.Empty.WithTenantId(" tenanta ").TenantId);
    }

    [Fact]
    public async Task PushCurrent_ShouldFlowAcrossAwaitAndRestorePreviousContext()
    {
        var previous = Context.Empty.WithTenantId("previous");
        var current = Context.Empty.WithTenantId("current");

        using (Context.PushCurrent(previous))
        {
            using (Context.PushCurrent(current))
            {
                await Task.Yield();
                Assert.Same(current, Context.Current);
            }

            Assert.Same(previous, Context.Current);
        }

        Assert.Same(Context.Empty, Context.Current);
    }

    [Fact]
    public async Task PushCurrent_ShouldIsolateParallelAsyncFlows()
    {
        var tenantIds = await Task.WhenAll(
            ObserveTenantAsync("tenanta"),
            ObserveTenantAsync("tenantb"));

        Assert.Equal(["tenanta", "tenantb"], tenantIds);
        Assert.Same(Context.Empty, Context.Current);

        static async Task<string> ObserveTenantAsync(string tenantId)
        {
            using var _ = Context.PushCurrent(Context.Empty.WithTenantId(tenantId));
            await Task.Yield();
            return Context.Current.TenantId;
        }
    }
}
