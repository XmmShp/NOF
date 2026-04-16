using Xunit;

namespace NOF.Abstraction.Tests;

public class EventPublisherTests
{
    [Fact]
    public async Task PublishEvent_ShouldDispatchToAmbientPublisher()
    {
        var publisher = new RecordingPublisher();
        var payload = new TestEvent("demo");

        using var _ = EventPublisher.PushCurrent(publisher);
        EventPublisher.PublishEvent(payload);

        await publisher.LastInvocation!;
        Assert.Same(payload, publisher.LastPayload);
        Assert.Contains(typeof(TestEvent), publisher.LastEventTypes!);
    }

    private sealed record TestEvent(string Value);

    private sealed class RecordingPublisher : IEventPublisher
    {
        public object? LastPayload { get; private set; }

        public Type[]? LastEventTypes { get; private set; }

        public Task? LastInvocation { get; private set; }

        public Task PublishAsync(object payload, Type[] eventTypes, CancellationToken cancellationToken)
        {
            LastPayload = payload;
            LastEventTypes = eventTypes;
            LastInvocation = Task.CompletedTask;
            return LastInvocation;
        }
    }
}
