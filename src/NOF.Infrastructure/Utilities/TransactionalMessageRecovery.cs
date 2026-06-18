using NOF.Application;

namespace NOF.Infrastructure;

internal static class TransactionalMessageRecovery
{
    private const string MaxRetryExceededError = "Exceeded max retry count";

    public static Task<int> MarkExpiredExhaustedInboxMessagesAsFailedAsync(
        IDbContext dbContext,
        int maxRetryCount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<NOFInboxMessage>()
            .Where(message => message.Status == InboxMessageStatus.Pending
                && message.RetryCount >= maxRetryCount
                && (message.ClaimedBy == null
                    || message.ClaimExpiresAtUtc == null
                    || message.ClaimExpiresAtUtc <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.Status, InboxMessageStatus.Failed)
                .SetProperty(message => message.ErrorMessage, MaxRetryExceededError)
                .SetProperty(message => message.FailedAtUtc, now)
                .SetProperty(message => message.ClaimedBy, (string?)null)
                .SetProperty(message => message.ClaimExpiresAtUtc, (DateTime?)null),
                cancellationToken);
    }

    public static Task<int> MarkExpiredExhaustedOutboxMessagesAsFailedAsync(
        IDbContext dbContext,
        int maxRetryCount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<NOFOutboxMessage>()
            .Where(message => message.Status == OutboxMessageStatus.Pending
                && message.RetryCount >= maxRetryCount
                && (message.ClaimedBy == null
                    || message.ClaimExpiresAtUtc == null
                    || message.ClaimExpiresAtUtc <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.Status, OutboxMessageStatus.Failed)
                .SetProperty(message => message.ErrorMessage, MaxRetryExceededError)
                .SetProperty(message => message.FailedAtUtc, now)
                .SetProperty(message => message.ClaimedBy, (string?)null)
                .SetProperty(message => message.ClaimExpiresAtUtc, (DateTime?)null),
                cancellationToken);
    }
}
