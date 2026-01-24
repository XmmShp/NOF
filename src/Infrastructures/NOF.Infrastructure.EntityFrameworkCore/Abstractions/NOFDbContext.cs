using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOF;

public record DbContextModelCreating(Type DbContextType, ModelBuilder Builder);

[Table(nameof(EFCoreStateMachineContext))]
internal sealed class EFCoreStateMachineContext
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

public class NOFDbContext : DbContext
{
    private readonly IStartupEventChannel _startupEventChannel;
    protected NOFDbContext(DbContextOptions options) : base(options)
    {
        var extension = options.FindExtension<NOFDbContextOptionsExtension>();
        _startupEventChannel = extension?.StartupEventChannel ?? throw new InvalidOperationException("EventDispatcher is not configured in NOFDbContextOptionsExtension.");
    }

    internal DbSet<EFCoreStateMachineContext> StateMachineContexts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EFCoreStateMachineContext>(entity =>
        {
            entity.HasKey(e => new { e.CorrelationId, e.DefinitionType });
        });

        _startupEventChannel.Publish(new DbContextModelCreating(GetType(), modelBuilder));
    }
}