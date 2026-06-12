using System.ComponentModel;

namespace NOF.Infrastructure;

/// <summary>
/// Outbox message entity used for adding messages in transactional context and reading by background services.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class NOFOutboxMessage
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }

    public OutboxMessageType MessageType { get; set; }
    public string PayloadType { get; set; } = null!;
    public string DispatchTypes { get; set; } = null!;
    public byte[] Payload { get; set; } = null!;
    public string Headers { get; set; } = null!;
    public DateTime? SentAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }

    public string? ClaimedBy { get; set; }
    public DateTime? ClaimExpiresAtUtc { get; set; }
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    public string? TraceParent { get; set; }
}

public enum OutboxMessageType
{
    Command = 0,
    Notification = 1
}

public enum OutboxMessageStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}
