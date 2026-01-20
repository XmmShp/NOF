namespace NOF;


// ReSharper disable once InconsistentNaming
public static partial class __NOF_Infrastructure_Core_Extensions__
{
    extension(IStartupEventChannel startupEventChannel)
    {
        public void Publish<TEvent>(TEvent @event, object? key = null) where TEvent : class
        {
            startupEventChannel.PublishAsync(@event, key).GetAwaiter().GetResult();
        }
    }
}
