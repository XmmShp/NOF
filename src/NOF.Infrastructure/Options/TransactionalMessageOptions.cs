namespace NOF.Infrastructure;

public sealed class TransactionalMessageOptions
{
    public TransactionalMessageProcessorOptions Inbox { get; set; } = new();

    public TransactionalMessageProcessorOptions Outbox { get; set; } = new();
}

public sealed class TransactionalMessageProcessorOptions
{
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 100;

    public int MaxRetryCount { get; set; } = 5;

    public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);
}
