using Microsoft.Extensions.Options;
using NOF.Contract;
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

    [Fact]
    public async Task DifferentMemoryHosts_ShouldNotShareInMemoryCacheState()
    {
        var hostA = CreateCacheService(
            new MemoryCacheServiceRider(new MemoryCacheServiceRiderState()),
            new CacheServiceLocalLockState());
        var hostB = CreateCacheService(
            new MemoryCacheServiceRider(new MemoryCacheServiceRiderState()),
            new CacheServiceLocalLockState());

        await hostA.SetAsync("shared-key", "host-a-value");

        var valueOnHostA = await hostA.GetAsync<string>("shared-key");
        var valueOnHostB = await hostB.GetAsync<string>("shared-key");

        Assert.True(valueOnHostA.HasValue);
        Assert.Equal("host-a-value", valueOnHostA.Value);
        Assert.False(valueOnHostB.HasValue);
    }

    private static CacheService CreateCacheService()
    {
        return CreateCacheService(new MemoryCacheServiceRider(), new CacheServiceLocalLockState());
    }

    private static CacheService CreateCacheService(
        ICacheServiceRider rider,
        CacheServiceLocalLockState localLockState)
    {
        var currentTenant = new CurrentTenant();
        _ = currentTenant.PushTenant(TenantId.Normalize("tenant-a"));

        return new CacheService(
            rider,
            new TestObjectSerializer(),
            new ExponentialBackoffCacheLockRetryStrategy(),
            Options.Create(new CacheServiceOptions
            {
                KeyPrefix = "tenant:{tenantId}:"
            }),
            currentTenant,
            localLockState);
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
