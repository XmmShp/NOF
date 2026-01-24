using Microsoft.EntityFrameworkCore;

namespace NOF;

/// <summary>
/// Public DbContext that is not isolated by tenant. Data is stored in {Database}Public database.
/// This prevents conflicts with tenants named "Public".
/// Contains system-wide entities like outbox and inbox messages.
/// </summary>
public abstract class NOFPublicDbContext : DbContext
{
    private readonly IStartupEventChannel _startupEventChannel;
    protected NOFPublicDbContext(DbContextOptions options) : base(options)
    {
        var extension = options.FindExtension<NOFDbContextOptionsExtension>();
        _startupEventChannel = extension?.StartupEventChannel ?? throw new InvalidOperationException("EventDispatcher is not configured in NOFDbContextOptionsExtension.");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        _startupEventChannel.Publish(new DbContextModelCreating(GetType(), modelBuilder));
    }
}
