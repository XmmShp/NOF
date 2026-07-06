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
    public void WithItems_ShouldMergeWithExistingItems()
    {
        var context = Context.Empty
            .WithItem("existing", "before")
            .WithItems(new Dictionary<object, object?>
            {
                ["existing"] = "after",
                ["added"] = 42
            });

        Assert.Equal("after", context["existing"]);
        Assert.Equal(42, context["added"]);
        Assert.Equal(2, context.Items.Count);
    }

    [Fact]
    public void WithItem_ShouldStoreItemWithoutSpecialTenantSemantics()
    {
        var context = Context.Empty.WithItem("NOF.TenantId", "tenant-a");

        Assert.Equal("tenant-a", context["NOF.TenantId"]);
    }

    [Fact]
    public void WithServiceToken_ShouldStoreTypedHeaderMarker()
    {
        var context = Context.Empty.WithServiceToken("Authorization");

        Assert.Equal("Authorization", context.GetServiceTokenHeaderName());
        Assert.True(context.TryGetItem(AuthenticationContextKeys.ServiceTokenHeader, out var value));
        Assert.Equal("Authorization", value);
    }

    [Fact]
    public void WithServiceToken_WithNullHeader_ShouldRemoveTypedHeaderMarker()
    {
        var context = Context.Empty
            .WithServiceToken("Authorization")
            .WithServiceToken(null);

        Assert.Null(context.GetServiceTokenHeaderName());
        Assert.False(context.TryGetItem(AuthenticationContextKeys.ServiceTokenHeader, out _));
    }

    [Fact]
    public void WithTokenExchange_ShouldStoreTypedHeaderMarker()
    {
        var context = Context.Empty.WithTokenExchange("X-Authorization");

        Assert.Equal(["X-Authorization"], context.GetTokenExchangeHeaderNames().OrderBy(static value => value, StringComparer.Ordinal).ToArray());
        Assert.True(context.TryGetItem(AuthenticationContextKeys.TokenExchangeHeaders, out var value));
        Assert.Equal(["X-Authorization"], Assert.IsType<HashSet<string>>(value).OrderBy(static value => value, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void WithTokenExchange_ShouldStoreMultipleDistinctHeaders()
    {
        var context = Context.Empty.WithTokenExchange("Authorization", "X-Authorization", "authorization", null, " ");

        Assert.Equal(["Authorization", "X-Authorization"], context.GetTokenExchangeHeaderNames().OrderBy(static value => value, StringComparer.Ordinal).ToArray());
        Assert.True(context.TryGetItem(AuthenticationContextKeys.TokenExchangeHeaders, out var value));
        Assert.Equal(["Authorization", "X-Authorization"], Assert.IsType<HashSet<string>>(value).OrderBy(static value => value, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void WithTokenExchange_WithNullHeader_ShouldRemoveTypedHeaderMarker()
    {
        var context = Context.Empty
            .WithTokenExchange("Authorization")
            .WithTokenExchange(null);

        Assert.Empty(context.GetTokenExchangeHeaderNames());
        Assert.False(context.TryGetItem(AuthenticationContextKeys.TokenExchangeHeaders, out _));
    }
}
