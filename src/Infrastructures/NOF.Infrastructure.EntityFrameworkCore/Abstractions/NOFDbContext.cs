using Microsoft.EntityFrameworkCore;

namespace NOF;

public record DbContextModelCreating(Type DbContextType, ModelBuilder Builder);

public abstract class NOFDbContext : DbContext
{
    private readonly IStartupEventChannel _startupEventChannel;
    protected NOFDbContext(DbContextOptions options) : base(options)
    {
        var extension = options.FindExtension<NOFDbContextOptionsExtension>();
        _startupEventChannel = extension?.StartupEventChannel ?? throw new InvalidOperationException("EventDispatcher is not configured in NOFDbContextOptionsExtension.");
    }

    internal DbSet<EFCoreStateMachineContext> StateMachineContexts { get; set; }
    internal DbSet<EFCoreInboxMessage> InboxMessages { get; set; }
    internal DbSet<EFCoreOutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EFCoreStateMachineContext>(entity =>
        {
            entity.HasKey(e => new { e.CorrelationId, e.DefinitionType });
        });

        modelBuilder.Entity<EFCoreInboxMessage>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<EFCoreOutboxMessage>(entity =>
        {
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasIndex(e => new { e.Status, e.ClaimExpiresAt });
            entity.HasIndex(e => e.ClaimedBy);
            entity.HasIndex(e => e.TraceId);
        });

        _startupEventChannel.Publish(new DbContextModelCreating(GetType(), modelBuilder));
    }
}