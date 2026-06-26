using NOF.Application;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public sealed class MemoryBackplaneTests
{
    [Fact]
    public async Task PublishAsync_ShouldDispatchToSubscribersOnSameChannel()
    {
        var backplane = new MemoryBackplane();
        string? received = null;

        await using var subscription = await backplane.SubscribeAsync<string>(
            "stream-updates",
            (payload, _) =>
            {
                received = payload;
                return ValueTask.CompletedTask;
            });

        await backplane.PublishAsync("stream-updates", "chunk-1");

        Assert.Equal("chunk-1", received);
    }

    [Fact]
    public async Task DisposeSubscription_ShouldStopReceivingPublishedMessages()
    {
        var backplane = new MemoryBackplane();
        var received = new List<string>();

        var subscription = await backplane.SubscribeAsync<string>(
            "stream-updates",
            (payload, _) =>
            {
                received.Add(payload);
                return ValueTask.CompletedTask;
            });

        await backplane.PublishAsync("stream-updates", "chunk-1");
        await subscription.DisposeAsync();
        await backplane.PublishAsync("stream-updates", "chunk-2");

        Assert.Equal(["chunk-1"], received);
    }

    [Fact]
    public async Task DifferentHosts_ShouldNotShareInMemoryBackplaneState()
    {
        var hostA = new MemoryBackplane(new MemoryBackplaneState());
        var hostB = new MemoryBackplane(new MemoryBackplaneState());
        var hostAReceived = new List<string>();
        var hostBReceived = new List<string>();

        await using var _ = await hostA.SubscribeAsync<string>(
            "stream-updates",
            (payload, _) =>
            {
                hostAReceived.Add(payload);
                return ValueTask.CompletedTask;
            });

        await using var __ = await hostB.SubscribeAsync<string>(
            "stream-updates",
            (payload, _) =>
            {
                hostBReceived.Add(payload);
                return ValueTask.CompletedTask;
            });

        await hostA.PublishAsync("stream-updates", "chunk-1");

        Assert.Equal(["chunk-1"], hostAReceived);
        Assert.Empty(hostBReceived);
    }
}
