using MassTransit;

namespace NOF;

[ExcludeFromTopology]
public interface INotificationHandler;

[ExcludeFromTopology]
public interface INotificationHandler<TNotification> : IConsumer<TNotification>, INotificationHandler
    where TNotification : class, INotification;
