using Microsoft.EntityFrameworkCore;

namespace NOF;

public record DbContextModelCreating(ModelBuilder Builder);

internal sealed class StateMachineContextInfo
{
    public string CorrelationId { get; set; } = null!;
    public string ContextType { get; set; } = null!;
    public string ContextData { get; set; } = null!;
}

public abstract class NOFDbContext : DbContext
{
    private readonly IEventDispatcher _eventDispatcher;
    protected NOFDbContext(DbContextOptions options) : base(options)
    {
        var extension = options.FindExtension<NOFDbContextOptionsExtension>();
        _eventDispatcher = extension?.EventDispatcher ?? throw new InvalidOperationException("EventDispatcher is not configured in NOFDbContextOptionsExtension.");
    }

    internal DbSet<StateMachineContextInfo> StateMachineContexts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StateMachineContextInfo>(entity =>
        {
            entity.HasKey(e => e.CorrelationId);
            entity.Property(e => e.ContextType).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.ContextData).IsRequired();
        });
        base.OnModelCreating(modelBuilder);
        _eventDispatcher.Publish(new DbContextModelCreating(modelBuilder));
    }
}
