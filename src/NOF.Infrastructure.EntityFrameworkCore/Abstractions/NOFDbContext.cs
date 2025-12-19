using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOF;

public record DbContextModelCreating(ModelBuilder Builder);

[Table(nameof(StateMachineContextInfo))]
internal sealed class StateMachineContextInfo
{
    public required string CorrelationId { get; set; }
    public required string DefinitionType { get; set; }
    public required string ContextType { get; set; }
    public required string ContextData { get; set; }
    public required int State { get; set; }
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
            entity.HasKey(e => new { e.CorrelationId, e.DefinitionType });
            entity.Property(e => e.ContextType).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.ContextData).IsRequired();
        });
        base.OnModelCreating(modelBuilder);
        _eventDispatcher.Publish(new DbContextModelCreating(modelBuilder));
    }
}
