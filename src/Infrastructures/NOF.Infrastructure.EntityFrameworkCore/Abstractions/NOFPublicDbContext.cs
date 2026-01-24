using Microsoft.EntityFrameworkCore;

namespace NOF;

public record PublicDbContextModelCreating(Type DbContextType, ModelBuilder Builder);

/// <summary>
/// Public DbContext that is not isolated by tenant. Data is stored in {Database}Public database.
/// This prevents conflicts with tenants named "Public".
/// </summary>
public abstract class NOFPublicDbContext : DbContext
{
    private readonly IStartupEventChannel _startupEventChannel;
    protected NOFPublicDbContext(DbContextOptions options) : base(options)
    {
        var extension = options.FindExtension<NOFDbContextOptionsExtension>();
        _startupEventChannel = extension?.StartupEventChannel ?? throw new InvalidOperationException("EventDispatcher is not configured in NOFDbContextOptionsExtension.");
    }

    internal DbSet<EFCoreTenant> Tenants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EFCoreTenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        _startupEventChannel.Publish(new PublicDbContextModelCreating(GetType(), modelBuilder));
    }
}
