using System.ComponentModel;

namespace NOF.Infrastructure;

/// <summary>
/// Outbox message entity used for adding messages in transactional context and reading by background services.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class NOFOutboxMessage
{
    private static long _lastCreatedAtUtcTicks;

    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }

    public OutboxMessageType MessageType { get; set; }
    public string DispatchRoutes { get; set; } = null!;
    public byte[] Payload { get; set; } = null!;
    public string Headers { get; set; } = null!;
    public DateTime? SentAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }

    public string? ClaimedBy { get; set; }
    public DateTime? ClaimExpiresAtUtc { get; set; }
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    public string? TraceParent { get; set; }
    public string? OrderKey { get; set; }
    public long? Sequence { get; set; }
    public bool CompletesOrderKey { get; set; }

    internal static NOFOutboxMessage Create(
        OutboxMessageType messageType,
        string dispatchRoutes,
        byte[] payload,
        string headers,
        string? traceParent,
        OutboxOrder? order = null)
    {
        var createdAtUtc = GetNextCreatedAtUtc();
        return new NOFOutboxMessage
        {
            Id = Guid.CreateVersion7(new DateTimeOffset(createdAtUtc)),
            CreatedAtUtc = createdAtUtc,
            MessageType = messageType,
            DispatchRoutes = dispatchRoutes,
            Payload = payload,
            Headers = headers,
            TraceParent = traceParent,
            OrderKey = order?.OrderKey,
            Sequence = order?.Sequence,
            CompletesOrderKey = order?.CompletesOrderKey ?? false
        };
    }

    private static DateTime GetNextCreatedAtUtc()
    {
        while (true)
        {
            // Use microsecond increments because common relational providers persist at least that precision.
            // This preserves enqueue order across command and notification senders in the same process.
            var nowTicks = DateTime.UtcNow.Ticks;
            nowTicks -= nowTicks % 10;
            var lastTicks = Volatile.Read(ref _lastCreatedAtUtcTicks);
            var nextTicks = Math.Max(nowTicks, lastTicks + 10);
            if (Interlocked.CompareExchange(ref _lastCreatedAtUtcTicks, nextTicks, lastTicks) == lastTicks)
            {
                return new DateTime(nextTicks, DateTimeKind.Utc);
            }
        }
    }
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
