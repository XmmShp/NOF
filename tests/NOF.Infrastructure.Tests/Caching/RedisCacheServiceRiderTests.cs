using Microsoft.Extensions.Caching.Distributed;
using Moq;
using NOF.Infrastructure.StackExchangeRedis;
using StackExchange.Redis;
using Xunit;

namespace NOF.Infrastructure.Tests.Caching;

public sealed class RedisCacheServiceRiderTests
{
    [Fact]
    public void Set_WithSlidingExpiration_ShouldPersistMetadataKey()
    {
        var database = new Mock<IDatabase>(MockBehavior.Strict);
        var connectionMultiplexer = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        connectionMultiplexer.Setup(static mock => mock.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        database.Setup(static mock => mock.StringSet(
                It.Is<RedisKey>(key => key == "cache-key"),
                It.IsAny<RedisValue>(),
                It.Is<TimeSpan?>(expiration => expiration == TimeSpan.FromMinutes(5)),
                false,
                When.Always,
                CommandFlags.None))
            .Returns(true);
        database.Setup(static mock => mock.StringSet(
                It.Is<RedisKey>(key => key == "cache-key::__nof:sliding-expiration"),
                It.IsAny<RedisValue>(),
                It.Is<TimeSpan?>(expiration => expiration == TimeSpan.FromMinutes(5)),
                false,
                When.Always,
                CommandFlags.None))
            .Returns(true);
        var rider = new RedisCacheServiceRider(connectionMultiplexer.Object, new StubTimeProvider(DateTimeOffset.UtcNow));

        rider.Set(
            "cache-key",
            [1, 2, 3],
            new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(5)
            });

        database.VerifyAll();
    }

    [Fact]
    public void Refresh_ShouldExtendSlidingExpirationFromMetadata()
    {
        var now = new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.Zero);
        var database = new Mock<IDatabase>(MockBehavior.Strict);
        var connectionMultiplexer = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        connectionMultiplexer.Setup(static mock => mock.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        database.Setup(static mock => mock.StringGet(
                It.Is<RedisKey>(key => key == "cache-key::__nof:sliding-expiration"),
                CommandFlags.None))
            .Returns(CreateMetadataValue(TimeSpan.FromMinutes(5), absoluteExpirationUtc: null));
        database.Setup(static mock => mock.KeyExpire(
                It.Is<RedisKey>(key => key == "cache-key"),
                It.Is<TimeSpan?>(expiration => expiration == TimeSpan.FromMinutes(5)),
                ExpireWhen.Always,
                CommandFlags.None))
            .Returns(true);
        database.Setup(static mock => mock.KeyExpire(
                It.Is<RedisKey>(key => key == "cache-key::__nof:sliding-expiration"),
                It.Is<TimeSpan?>(expiration => expiration == TimeSpan.FromMinutes(5)),
                ExpireWhen.Always,
                CommandFlags.None))
            .Returns(true);
        var rider = new RedisCacheServiceRider(connectionMultiplexer.Object, new StubTimeProvider(now));

        rider.Refresh("cache-key");

        database.VerifyAll();
    }

    [Fact]
    public async Task RefreshAsync_ShouldCapSlidingExpirationByAbsoluteExpiration()
    {
        var now = new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.Zero);
        var absoluteExpiration = now.AddSeconds(15);
        var database = new Mock<IDatabase>(MockBehavior.Strict);
        var connectionMultiplexer = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        connectionMultiplexer.Setup(static mock => mock.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        database.Setup(static mock => mock.StringGetAsync(
                It.Is<RedisKey>(key => key == "cache-key::__nof:sliding-expiration"),
                CommandFlags.None))
            .ReturnsAsync(CreateMetadataValue(TimeSpan.FromMinutes(5), absoluteExpiration));
        database.Setup(static mock => mock.KeyExpireAsync(
                It.Is<RedisKey>(key => key == "cache-key"),
                It.Is<TimeSpan?>(expiration => expiration == TimeSpan.FromSeconds(15)),
                It.IsAny<ExpireWhen>(),
                CommandFlags.None))
            .ReturnsAsync(true);
        database.Setup(static mock => mock.KeyExpireAsync(
                It.Is<RedisKey>(key => key == "cache-key::__nof:sliding-expiration"),
                It.Is<TimeSpan?>(expiration => expiration == TimeSpan.FromSeconds(15)),
                It.IsAny<ExpireWhen>(),
                CommandFlags.None))
            .ReturnsAsync(true);
        var rider = new RedisCacheServiceRider(connectionMultiplexer.Object, new StubTimeProvider(now));

        await rider.RefreshAsync("cache-key");

        database.VerifyAll();
    }

    [Fact]
    public void Set_ShouldUseConfiguredExpiration()
    {
        var database = new Mock<IDatabase>(MockBehavior.Strict);
        var connectionMultiplexer = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        connectionMultiplexer.Setup(static mock => mock.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        database.Setup(static mock => mock.StringSet(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Returns(true);
        database.Setup(static mock => mock.KeyDelete(
                It.Is<RedisKey>(key => key == "cache-key::__nof:sliding-expiration"),
                CommandFlags.None))
            .Returns(true);
        var rider = new RedisCacheServiceRider(connectionMultiplexer.Object);

        rider.Set(
            "cache-key",
            [1, 2, 3],
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

        database.Verify(static mock => mock.StringSet(
            It.Is<RedisKey>(key => key == "cache-key"),
            It.IsAny<RedisValue>(),
            It.Is<TimeSpan?>(expiration => expiration == TimeSpan.FromMinutes(5)),
            false,
            When.Always,
            CommandFlags.None), Times.Once);
        database.Verify(static mock => mock.KeyDelete(
            It.Is<RedisKey>(key => key == "cache-key::__nof:sliding-expiration"),
            CommandFlags.None), Times.Once);
    }

    private static byte[] CreateMetadataValue(TimeSpan slidingExpiration, DateTimeOffset? absoluteExpirationUtc)
        => System.Text.Encoding.UTF8.GetBytes($"{slidingExpiration.Ticks}|{absoluteExpirationUtc?.UtcDateTime.Ticks.ToString() ?? string.Empty}");

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
