using Moq;
using NOF.Infrastructure.StackExchangeRedis;
using StackExchange.Redis;
using System.Text.Json;
using Xunit;

namespace NOF.Infrastructure.Tests.Caching;

public sealed class RedisBackplaneTests
{
    private static readonly JsonObjectSerializer _serializer = new();

    [Fact]
    public async Task PublishAsync_ShouldSerializeEnvelopeAndPublishToChannel()
    {
        var subscriber = new Mock<ISubscriber>(MockBehavior.Strict);
        var connectionMultiplexer = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);

        connectionMultiplexer.Setup(static mock => mock.GetSubscriber(It.IsAny<object>()))
            .Returns(subscriber.Object);
        subscriber.Setup(static mock => mock.PublishAsync(
                It.Is<RedisChannel>(channel => channel == "stream-updates"),
                It.IsAny<RedisValue>(),
                CommandFlags.None))
            .ReturnsAsync(1)
            .Callback<RedisChannel, RedisValue, CommandFlags>((_, value, _) =>
            {
                using var document = JsonDocument.Parse((byte[])value!);
                Assert.Equal(typeof(TestMessage).AssemblyQualifiedName, document.RootElement.GetProperty("payloadType").GetString());
                Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("payload").GetString()));
            });

        var backplane = new RedisBackplane(connectionMultiplexer.Object, _serializer);

        await backplane.PublishAsync("stream-updates", new TestMessage("chunk-1"));

        subscriber.VerifyAll();
    }

    [Fact]
    public async Task SubscribeAsync_ShouldDispatchMatchingPayloadsAndUnsubscribeOnDispose()
    {
        var subscriber = new Mock<ISubscriber>(MockBehavior.Strict);
        var connectionMultiplexer = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        Action<RedisChannel, RedisValue>? callback = null;

        connectionMultiplexer.Setup(static mock => mock.GetSubscriber(It.IsAny<object>()))
            .Returns(subscriber.Object);
        subscriber.Setup(static mock => mock.SubscribeAsync(
                It.Is<RedisChannel>(channel => channel == "stream-updates"),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                CommandFlags.None))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>((_, handler, _) => callback = handler)
            .Returns(Task.CompletedTask);
        subscriber.Setup(static mock => mock.UnsubscribeAsync(
                It.Is<RedisChannel>(channel => channel == "stream-updates"),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                CommandFlags.None))
            .Returns(Task.CompletedTask);

        var backplane = new RedisBackplane(connectionMultiplexer.Object, _serializer);
        var received = new List<string>();

        await using var subscription = await backplane.SubscribeAsync<TestMessage>(
            "stream-updates",
            (payload, _) =>
            {
                received.Add(payload.Value);
                return ValueTask.CompletedTask;
            });

        Assert.NotNull(callback);
        callback!(
            RedisChannel.Literal("stream-updates"),
            CreateEnvelope(new TestMessage("chunk-1")));
        callback!(
            RedisChannel.Literal("stream-updates"),
            CreateEnvelope("wrong-type"));

        Assert.Equal(["chunk-1"], received);

        await subscription.DisposeAsync();

        subscriber.VerifyAll();
    }

    private static byte[] CreateEnvelope<T>(T payload)
    {
        var runtimeType = payload!.GetType();
        var payloadBytes = _serializer.Serialize(payload, runtimeType).ToArray();

        return _serializer.Serialize(new Envelope
        {
            PayloadType = runtimeType.AssemblyQualifiedName ?? runtimeType.FullName ?? runtimeType.Name,
            Payload = payloadBytes
        }, typeof(Envelope)).ToArray();
    }

    private sealed record TestMessage(string Value);

    private sealed class Envelope
    {
        public required string PayloadType { get; set; }

        public required byte[] Payload { get; set; }
    }
}
