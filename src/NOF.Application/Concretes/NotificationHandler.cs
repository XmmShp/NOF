using NOF.Application.Reflections;

namespace NOF;

public interface INotificationHandler<in TNotification> : INotificationHandler
    where TNotification : class, INotification
{
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken);
}
