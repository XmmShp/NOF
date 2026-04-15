using Xunit;

namespace NOF.Abstraction.Tests;

public class EventHandlerInfosTests
{
    [Fact]
    public void GetHandlerTypes_ShouldPreferMostSpecificAndDeDupeHandlers()
    {
        var infos = new EventHandlerInfos();
        infos.Add(new EventHandlerRegistration(typeof(MultiHandler), typeof(object)));
        infos.Add(new EventHandlerRegistration(typeof(MultiHandler), typeof(MyEvent)));
        infos.Add(new EventHandlerRegistration(typeof(InterfaceHandler), typeof(IMarkerEvent)));

        var handlers = infos.GetHandlerTypes(typeof(MyEvent));

        Assert.Contains(typeof(InterfaceHandler), handlers);
        Assert.Contains(typeof(MultiHandler), handlers);
        Assert.Single(handlers, static handler => handler == typeof(MultiHandler));
    }

    [Fact]
    public void Events_FirstReadShouldImportStaticRegistryAndFreeze()
    {
        EventHandlerRegistry.Register(new EventHandlerRegistration(typeof(RegistryHandler), typeof(RegistryEvent)));

        var infos = new EventHandlerInfos();

        Assert.Contains(infos.Events, registration =>
            registration.HandlerType == typeof(RegistryHandler) &&
            registration.EventType == typeof(RegistryEvent));

        Assert.Throws<InvalidOperationException>(() =>
            infos.Add(new EventHandlerRegistration(typeof(PostFreezeHandler), typeof(PostFreezeEvent))));
    }

    private interface IMarkerEvent;

    private record MyEvent : IMarkerEvent;
    private record RegistryEvent;
    private record PostFreezeEvent;

    private sealed class MultiHandler;

    private sealed class InterfaceHandler;
    private sealed class RegistryHandler;
    private sealed class PostFreezeHandler;
}
