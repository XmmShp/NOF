using NOF.Application.Annotations;

namespace NOF;

public interface INotificationHandler<in TNotification> : INotificationHandler
    where TNotification : class, INotification
{
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken);
}
