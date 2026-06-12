using Xunit;

namespace NOF.Abstraction.Tests;

public class EventHandlerRegistryTests
{
    [Fact]
    public void GetHandlerTypes_ShouldReturnOnlyLiteralRegistrations()
    {
        var registry = new EventHandlerRegistry();
        registry.Add(new EventHandlerRegistration(typeof(MultiHandler), typeof(object)));
        registry.Add(new EventHandlerRegistration(typeof(MultiHandler), typeof(MyEvent)));
        registry.Add(new EventHandlerRegistration(typeof(InterfaceHandler), typeof(IMarkerEvent)));

        var handlers = registry.GetHandlerTypes(typeof(MyEvent));

        Assert.Contains(typeof(MultiHandler), handlers);
        Assert.Single(handlers, static handler => handler == typeof(MultiHandler));
        Assert.DoesNotContain(typeof(InterfaceHandler), handlers);
    }

    [Fact]
    public void Events_FirstReadShouldFreezeRegistry()
    {
        var registry = new EventHandlerRegistry();
        registry.Add(new EventHandlerRegistration(typeof(MultiHandler), typeof(MyEvent)));

        _ = registry.Freeze();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Add(new EventHandlerRegistration(typeof(RegistryHandler), typeof(RegistryEvent))));
    }

    [Fact]
    public void EventHandlerRegistry_ShouldStoreRegistrations()
    {
        var registry = new EventHandlerRegistry();
        registry.Add(new EventHandlerRegistration(typeof(RegistryHandler), typeof(RegistryEvent)));

        Assert.Contains(registry.Freeze(), registration =>
            registration.HandlerType == typeof(RegistryHandler) &&
            registration.EventType == typeof(RegistryEvent));
    }

    [Fact]
    public void RemoveWhere_ShouldRebuildEventHandlerIndex()
    {
        var registry = new EventHandlerRegistry();
        registry.Add(new EventHandlerRegistration(typeof(MultiHandler), typeof(MyEvent)));
        registry.Add(new EventHandlerRegistration(typeof(RegistryHandler), typeof(MyEvent)));

        var removedCount = registry.RemoveWhere(static registration => registration.HandlerType == typeof(MultiHandler));
        var handlers = registry.GetHandlerTypes(typeof(MyEvent));

        Assert.Equal(1, removedCount);
        Assert.DoesNotContain(typeof(MultiHandler), handlers);
        Assert.Contains(typeof(RegistryHandler), handlers);
    }

    private interface IMarkerEvent;

    private record MyEvent : IMarkerEvent;
    private record RegistryEvent;

    private sealed class MultiHandler;

    private sealed class InterfaceHandler;
    private sealed class RegistryHandler;
}
