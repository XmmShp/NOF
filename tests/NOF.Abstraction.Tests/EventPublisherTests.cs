using NOF.Contract;
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
        Assert.Same(Context.Empty, publisher.LastContext);
    }

    [Fact]
    public async Task PublishAsEvent_WithExplicitPublisher_ShouldDispatchWithoutAmbientScope()
    {
        var publisher = new RecordingPublisher();
        var payload = new TestEvent("explicit");

        payload.PublishAsEvent(publisher);

        await publisher.LastInvocation!;
        Assert.Same(payload, publisher.LastPayload);
        Assert.Contains(typeof(TestEvent), publisher.LastEventTypes!);
        Assert.Same(Context.Empty, publisher.LastContext);
    }

    [Fact]
    public async Task PublishAsEvent_ShouldForwardContextBoundToAmbientPublisher()
    {
        var publisher = new RecordingPublisher();
        var payload = new TestEvent("context");
        var context = Context.Empty.WithItem("correlation-id", "42");

        using var publisherScope = EventPublisher.PushCurrent(publisher);
        using var contextScope = EventPublisher.PushContext(context);
        payload.PublishAsEvent();

        await publisher.LastInvocation!;
        Assert.Same(context, publisher.LastContext);
    }

    [Fact]
    public async Task PublishAsEvent_WithExplicitPublisherAndContext_ShouldForwardSameContext()
    {
        var publisher = new RecordingPublisher();
        var payload = new TestEvent("explicit-context");
        var context = Context.Empty.WithItem("correlation-id", "42");

        payload.PublishAsEvent(publisher, context);

        await publisher.LastInvocation!;
        Assert.Same(context, publisher.LastContext);
    }

    [Fact]
    public async Task InMemoryEventPublisher_ShouldForwardSameContextToHandler()
    {
        var payload = new TestEvent("handler");
        var context = Context.Empty.WithItem("correlation-id", "42");
        var handler = new RecordingHandler();
        var registry = new EventHandlerRegistry();
        registry.Add(new EventHandlerRegistration(typeof(RecordingHandler), typeof(TestEvent)));
        var publisher = new InMemoryEventPublisher(new TestServiceProvider(handler), registry);

        await publisher.PublishAsync(payload, context);

        Assert.Same(payload, handler.LastEvent);
        Assert.Same(context, handler.LastContext);
    }

    [Fact]
    public async Task InMemoryEventPublisher_ShouldPreserveContextAcrossSecondaryEvents()
    {
        var context = Context.Empty.WithItem("correlation-id", "42");
        var primaryHandler = new SecondaryEventPublishingHandler();
        var secondaryHandler = new SecondaryEventRecordingHandler();
        var registry = new EventHandlerRegistry();
        registry.Add(new EventHandlerRegistration(typeof(SecondaryEventPublishingHandler), typeof(PrimaryEvent)));
        registry.Add(new EventHandlerRegistration(typeof(SecondaryEventRecordingHandler), typeof(SecondaryEvent)));
        var publisher = new InMemoryEventPublisher(
            new TestServiceProvider(primaryHandler, secondaryHandler),
            registry);

        await publisher.PublishAsync(new PrimaryEvent("primary"), context);

        Assert.Equal("primary-secondary", secondaryHandler.LastEvent?.Value);
        Assert.Same(context, secondaryHandler.LastContext);
    }

    private sealed record TestEvent(string Value);

    private sealed record PrimaryEvent(string Value);

    private sealed record SecondaryEvent(string Value);

    private sealed class RecordingPublisher : IEventPublisher
    {
        public object? LastPayload { get; private set; }

        public Type[]? LastEventTypes { get; private set; }

        public Context? LastContext { get; private set; }

        public Task? LastInvocation { get; private set; }

        public Task PublishAsync(
            object payload,
            Type[] eventTypes,
            Context context,
            CancellationToken cancellationToken)
        {
            LastPayload = payload;
            LastEventTypes = eventTypes;
            LastContext = context;
            LastInvocation = Task.CompletedTask;
            return LastInvocation;
        }
    }

    private sealed class RecordingHandler : InMemoryEventHandler<TestEvent>
    {
        public TestEvent? LastEvent { get; private set; }

        public Context? LastContext { get; private set; }

        public override Task HandleAsync(TestEvent @event, Context context, CancellationToken cancellationToken)
        {
            LastEvent = @event;
            LastContext = context;
            return Task.CompletedTask;
        }
    }

    private sealed class SecondaryEventPublishingHandler : InMemoryEventHandler<PrimaryEvent>
    {
        public override Task HandleAsync(PrimaryEvent @event, Context context, CancellationToken cancellationToken)
        {
            new SecondaryEvent($"{@event.Value}-secondary").PublishAsEvent();
            return Task.CompletedTask;
        }
    }

    private sealed class SecondaryEventRecordingHandler : InMemoryEventHandler<SecondaryEvent>
    {
        public SecondaryEvent? LastEvent { get; private set; }

        public Context? LastContext { get; private set; }

        public override Task HandleAsync(SecondaryEvent @event, Context context, CancellationToken cancellationToken)
        {
            LastEvent = @event;
            LastContext = context;
            return Task.CompletedTask;
        }
    }

    private sealed class TestServiceProvider(params object[] services) : IServiceProvider
    {
        private readonly IReadOnlyDictionary<Type, object> _services =
            services.ToDictionary(static service => service.GetType());

        public object? GetService(Type serviceType)
            => _services.GetValueOrDefault(serviceType);
    }
}
