using NOF.Application;
using NOF.Contract;

namespace NOF.Sample.Application.Integration;

/// <summary>Compilable example of application persistence and transactional dispatch.</summary>
public static class TransactionalMessages
{
    public sealed record OrderCreatedNotification(Guid OrderId);
    public sealed record SendEmailCommand(string To, string Subject, string Body);

    public static async Task StageAsync(
        IDbContext dbContext,
        INotificationPublisher notificationPublisher,
        ICommandSender commandSender,
        Guid orderId,
        string email,
        Context context,
        CancellationToken cancellationToken)
    {
        // Persist business changes using this same dbContext before saving.
        await notificationPublisher.DeferPublishAsync(new OrderCreatedNotification(orderId), context, cancellationToken);
        await commandSender.DeferSendAsync(new SendEmailCommand(email, "Created", "Order created."), context, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
