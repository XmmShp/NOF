namespace NOF;


// ReSharper disable once InconsistentNaming
public static partial class __NOF_Integration_Extensions__
{
    extension(IEventDispatcher eventDispatcher)
    {
        public void Publish<TEvent>(TEvent @event, object? key = null) where TEvent : class
        {
            eventDispatcher.PublishAsync(@event, key).GetAwaiter().GetResult();
        }
    }
}
