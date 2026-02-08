using Microsoft.EntityFrameworkCore;

namespace NOF;

public abstract class NOFDbContext : DbContext
{
    private readonly DbContextOptions _options;

    protected NOFDbContext(DbContextOptions options) : base(options)
    {
        _options = options;
    }

    internal DbSet<EFCoreStateMachineContext> StateMachineContexts { get; set; }
    internal DbSet<EFCoreInboxMessage> InboxMessages { get; set; }
    internal DbSet<EFCoreOutboxMessage> OutboxMessages { get; set; }
    internal DbSet<EFCoreTenant> Tenants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply host-only filtering in tenant contexts (when NOFTenantDbContextOptionsExtension is present)
        if (_options.FindExtension<NOFTenantDbContextOptionsExtension>() != null)
        {
            OnTenantModelCreating(modelBuilder);
        }
    }

    protected virtual void OnTenantModelCreating(ModelBuilder modelBuilder)
    {
        var customizer = new IgnoreHostOnlyModelCustomizer();
        customizer.Customize(modelBuilder);
    }
}