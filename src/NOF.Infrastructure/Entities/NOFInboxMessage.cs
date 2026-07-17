namespace NOF.Infrastructure;

public class NOFInboxMessage
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }

    public InboxMessageType MessageType { get; set; }
    public string Route { get; set; } = null!;
    public byte[] Payload { get; set; } = null!;
    public string Headers { get; set; } = null!;
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }

    public string? ClaimedBy { get; set; }
    public DateTime? ClaimExpiresAtUtc { get; set; }
    public InboxMessageStatus Status { get; set; } = InboxMessageStatus.Pending;
}

public enum InboxMessageType
{
    Command = 0,
    Notification = 1
}

public enum InboxMessageStatus
{
    Pending = 0,
    Processed = 1,
    Failed = 2
}
