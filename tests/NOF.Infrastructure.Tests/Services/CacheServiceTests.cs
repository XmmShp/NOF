using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Application;
using System.Text.Json;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public sealed class CacheServiceTests
{
    [Fact]
    public async Task IgnoreQueryFilters_ShouldBypassConfiguredKeyPrefix()
    {
        var cacheService = CreateCacheService();
        var sharedCache = cacheService.IgnoreQueryFilters();

        await cacheService.SetAsync("prefixed-key", "prefixed-value");
        await sharedCache.SetAsync("shared-key", "shared-value");

        var prefixedViaDefault = await cacheService.GetAsync<string>("prefixed-key");
        var prefixedViaShared = await sharedCache.GetAsync<string>("prefixed-key");
        var sharedViaDefault = await cacheService.GetAsync<string>("shared-key");
        var sharedViaShared = await sharedCache.GetAsync<string>("shared-key");

        Assert.True(prefixedViaDefault.HasValue);
        Assert.Equal("prefixed-value", prefixedViaDefault.Value);
        Assert.False(prefixedViaShared.HasValue);
        Assert.False(sharedViaDefault.HasValue);
        Assert.True(sharedViaShared.HasValue);
        Assert.Equal("shared-value", sharedViaShared.Value);
    }

    private static CacheService CreateCacheService()
    {
        return new CacheService(
            new MemoryCacheServiceRider(),
            new TestObjectSerializer(),
            new ExponentialBackoffCacheLockRetryStrategy(),
            Options.Create(new CacheServiceOptions
            {
                KeyPrefix = "tenant:{tenantId}:"
            }),
            new TransparentInfos
            {
                TenantId = TenantId.Normalize("tenant-a")
            });
    }

    private sealed class TestObjectSerializer : IObjectSerializer
    {
        public ReadOnlyMemory<byte> Serialize(object? value, Type? runtimeType = null)
        {
            if (value is null)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            return JsonSerializer.SerializeToUtf8Bytes(value, runtimeType ?? value.GetType());
        }

        public object? Deserialize(ReadOnlyMemory<byte> data, Type runtimeType)
        {
            if (data.IsEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize(data.Span, runtimeType);
        }
    }
}
