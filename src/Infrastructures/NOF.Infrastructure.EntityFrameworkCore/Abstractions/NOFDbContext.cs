using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace NOF;

public record DbContextModelCreating(ModelBuilder Builder);

[Table(nameof(StateMachineContextInfo))]
internal sealed class StateMachineContextInfo
{
    [Required]
    public required string CorrelationId { get; set; }

    [Required]
    public required string DefinitionType { get; set; }

    [Required]
    [MaxLength(1024)]
    public required string ContextType { get; set; }

    [Required]
    public required string ContextData { get; set; }

    public required int State { get; set; }
}

[Table(nameof(TransactionalMessage))]
internal sealed class TransactionalMessage
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public OutboxMessageType MessageType { get; set; }

    [Required]
    [MaxLength(512)]
    public string PayloadType { get; set; } = null!;

    [Required]
    public string Payload { get; set; } = null!;

    [MaxLength(256)]
    public string? DestinationEndpointName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }

    [MaxLength(2048)]
    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    public OutboxMessageStatus Status { get; set; }
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


public abstract class NOFDbContext : DbContext
{
    private readonly IStartupEventChannel _startupEventChannel;
    protected NOFDbContext(DbContextOptions options) : base(options)
    {
        var extension = options.FindExtension<NOFDbContextOptionsExtension>();
        _startupEventChannel = extension?.StartupEventChannel ?? throw new InvalidOperationException("EventDispatcher is not configured in NOFDbContextOptionsExtension.");
    }

    internal DbSet<StateMachineContextInfo> StateMachineContexts { get; set; }
    internal DbSet<TransactionalMessage> TransactionalMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StateMachineContextInfo>(entity =>
        {
            entity.HasKey(e => new { e.CorrelationId, e.DefinitionType });
        });

        modelBuilder.Entity<TransactionalMessage>(entity =>
        {
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
        });

        base.OnModelCreating(modelBuilder);
        _startupEventChannel.Publish(new DbContextModelCreating(modelBuilder));
    }
}

internal class NOFDbContextOptionsExtension : IDbContextOptionsExtension
{
    public IStartupEventChannel StartupEventChannel { get; }

    public NOFDbContextOptionsExtension(IStartupEventChannel startupEventChannel)
    {
        StartupEventChannel = startupEventChannel ?? throw new ArgumentNullException(nameof(startupEventChannel));
    }

    public void ApplyServices(IServiceCollection services) { }

    public void Validate(IDbContextOptions options) { }

    public DbContextOptionsExtensionInfo Info => new NoFDbContextOptionsExtensionInfo(this);

    private sealed class NoFDbContextOptionsExtensionInfo : DbContextOptionsExtensionInfo
    {
        private readonly NOFDbContextOptionsExtension _extension;

        public NoFDbContextOptionsExtensionInfo(IDbContextOptionsExtension extension)
            : base(extension)
        {
            _extension = (NOFDbContextOptionsExtension)extension;
        }

        public override bool IsDatabaseProvider => false;
        public override string LogFragment => "";

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
        {
            return other is NoFDbContextOptionsExtensionInfo otherTyped &&
                   ReferenceEquals(_extension.StartupEventChannel, otherTyped._extension.StartupEventChannel);
        }

        public override int GetServiceProviderHashCode()
        {
            return RuntimeHelpers.GetHashCode(_extension.StartupEventChannel);
        }

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) { }
    }
}
