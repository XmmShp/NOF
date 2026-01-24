using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOF;

[Table(nameof(EFCoreOutboxMessage))]
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

    public string Headers { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }

    [MaxLength(2048)]
    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    /// <summary>
    /// 抢占锁标识符（实例ID）
    /// </summary>
    [MaxLength(256)]
    public string? ClaimedBy { get; set; }

    /// <summary>
    /// 抢占锁过期时间
    /// </summary>
    public DateTimeOffset? ClaimExpiresAt { get; set; }

    public OutboxMessageStatus Status { get; set; }

    /// <summary>
    /// 分布式追踪 TraceId（用于恢复追踪上下文）
    /// </summary>
    [MaxLength(128)]
    public string? TraceId { get; set; }

    /// <summary>
    /// 分布式追踪 SpanId（用于恢复追踪上下文）
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

/// <summary>
/// 收件箱消息实体
/// 用于记录需要可靠处理的消息
/// </summary>
[Table(nameof(EFCoreInboxMessage))]
internal sealed class EFCoreInboxMessage
{
    /// <summary>
    /// 消息唯一标识
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// 消息创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Public DbContext that is not isolated by tenant. Data is stored in {Database}Public database.
/// This prevents conflicts with tenants named "Public".
/// Contains system-wide entities like outbox and inbox messages.
/// </summary>
public class NOFPublicDbContext : NOFDbContext
{
    private readonly IStartupEventChannel _startupEventChannel;
    protected NOFPublicDbContext(DbContextOptions options) : base(options)
    {
        var extension = options.FindExtension<NOFDbContextOptionsExtension>();
        _startupEventChannel = extension?.StartupEventChannel ?? throw new InvalidOperationException("EventDispatcher is not configured in NOFDbContextOptionsExtension.");
    }

    internal DbSet<EFCoreOutboxMessage> OutboxMessages { get; set; }
    internal DbSet<EFCoreInboxMessage> InboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EFCoreOutboxMessage>(entity =>
        {
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasIndex(e => new { e.Status, e.ClaimExpiresAt });
            entity.HasIndex(e => e.ClaimedBy);
            entity.HasIndex(e => e.TraceId);
        });

        modelBuilder.Entity<EFCoreInboxMessage>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt);
        });

        _startupEventChannel.Publish(new DbContextModelCreating(GetType(), modelBuilder));
    }
}
