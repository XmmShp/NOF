using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOF;

[Table(nameof(EFCoreOutboxMessage))]
[Index(nameof(Status), nameof(CreatedAt))]
[Index(nameof(Status), nameof(ClaimExpiresAt))]
[Index(nameof(ClaimedBy))]
[Index(nameof(TraceId))]
internal sealed class EFCoreOutboxMessage
{
    [Key]
    public long Id { get; set; }

    [Required]
    public OutboxMessageType MessageType { get; set; }

    [Required]
    [MaxLength(512)]
    public string PayloadType { get; set; } = null!;

    [Required]
    public string Payload { get; set; } = null!;

    [MaxLength(256)]
    public string? DestinationEndpointName { get; set; }

    public string Headers { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }

    [MaxLength(2048)]
    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    /// <summary>
    /// The claim lock identifier (instance ID).
    /// </summary>
    [MaxLength(256)]
    public string? ClaimedBy { get; set; }

    /// <summary>
    /// The claim lock expiration time.
    /// </summary>
    public DateTimeOffset? ClaimExpiresAt { get; set; }

    public OutboxMessageStatus Status { get; set; }

    /// <summary>
    /// Distributed tracing TraceId (used to restore tracing context).
    /// </summary>
    [MaxLength(128)]
    public string? TraceId { get; set; }

    /// <summary>
    /// Distributed tracing SpanId (used to restore tracing context).
    /// </summary>
    [MaxLength(128)]
    public string? SpanId { get; set; }
}

internal enum OutboxMessageType
{
    Command = 0,
    Notification = 1
}

internal enum OutboxMessageStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}
