using NOF.Contract;
using NOF.Domain;
using System.ComponentModel;

namespace NOF.Infrastructure;

/// <summary>
/// Outbox message entity used for adding messages in transactional context and reading by background services.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class NOFOutboxMessage : AggregateRoot, ICloneable
{
    /// <summary>
    /// The message ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The creation time.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The retry count (defaults to 0 when added).
    /// </summary>
    public int RetryCount { get; set; }

    public OutboxMessageType MessageType { get; set; }
    public string PayloadType { get; set; } = null!;
    public byte[] Payload { get; set; } = null!;
    public string Headers { get; set; } = null!;
    public DateTime? SentAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The claim lock identifier (instance ID).
    /// </summary>
    public string? ClaimedBy { get; set; }

    /// <summary>
    /// The claim lock expiration time.
    /// </summary>
    public DateTime? ClaimExpiresAt { get; set; }

    public OutboxMessageStatus Status { get; set; }
    public TracingInfo? ParentTracingInfo { get; set; }

    public object Clone()
        => new NOFOutboxMessage
        {
            Id = Id,
            CreatedAt = CreatedAt,
            RetryCount = RetryCount,
            MessageType = MessageType,
            PayloadType = PayloadType,
            Payload = [.. Payload],
            Headers = Headers,
            SentAt = SentAt,
            FailedAt = FailedAt,
            ErrorMessage = ErrorMessage,
            ClaimedBy = ClaimedBy,
            ClaimExpiresAt = ClaimExpiresAt,
            Status = Status,
            ParentTracingInfo = ParentTracingInfo is null ? null : new TracingInfo(ParentTracingInfo.TraceId, ParentTracingInfo.SpanId)
        };
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
