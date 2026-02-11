using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure.EntityFrameworkCore;

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

    /// <summary>
    /// Override this method to specify additional entity types that should be ignored
    /// in tenant mode, beyond those marked with <see cref="HostOnlyAttribute"/>.
    /// </summary>
    protected virtual Type[] GetTenantIgnoredEntityTypes() => [];

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // In tenant mode, register a finalizing convention that removes [HostOnly] entities
        // after all OnModelCreating configurations have been applied
        var tenantExtension = _options.FindExtension<NOFTenantDbContextOptionsExtension>();
        if (tenantExtension is not null && !string.IsNullOrWhiteSpace(tenantExtension.TenantId))
        {
            var additionalIgnoredTypes = GetTenantIgnoredEntityTypes();
            tenantExtension.TenantIgnoredEntityTypes = additionalIgnoredTypes;
            configurationBuilder.Conventions.Add(_ => new HostOnlyModelFinalizingConvention(additionalIgnoredTypes));
        }
    }
}
