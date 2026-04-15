using NOF.Abstraction;
using NOF.Hosting;
using NOF.Infrastructure.EntityFrameworkCore;
using NOF.Infrastructure.EntityFrameworkCore.SQLite;

namespace NOF.Infrastructure.Memory;

public static class NOFInfrastructureMemoryExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddMemoryInfrastructure()
        {
            return builder.AddMemoryInfrastructure<NOFDbContext>();
        }

        public INOFAppBuilder AddMemoryInfrastructure<TDbContext>(TenantMode tenantMode = TenantMode.SingleTenant, string databaseName = "nof-sqlite-memory")
            where TDbContext : NOFDbContext
        {
            builder.Services.ReplaceOrAddCacheService<MemoryCacheService>();

            builder.Services.ReplaceOrAddScoped<IEventPublisher, EventPublisher>();

            builder.Services.ReplaceOrAddSingleton<ICommandRider, MemoryCommandRider>();
            builder.Services.ReplaceOrAddSingleton<INotificationRider, MemoryNotificationRider>();

            var selector = builder.AddEFCore<TDbContext>();
            selector = tenantMode switch
            {
                TenantMode.SharedDatabase => selector.UseSharedDatabaseTenancy(),
                TenantMode.DatabasePerTenant => selector.UseDatabasePerTenant(),
                _ => selector.UseSingleTenant()
            };

            selector
                .AutoMigrate()
                .UseSqliteInMemory(databaseName);
            return builder;
        }

        [Obsolete("Use AddMemoryInfrastructure<TDbContext>() instead. This method will be removed in a future version.")]
        public INOFAppBuilder AddMemoryInfrastructureWithSqlite<TDbContext>(TenantMode tenantMode = TenantMode.SingleTenant, string databaseName = "nof-sqlite-memory")
            where TDbContext : NOFDbContext
            => builder.AddMemoryInfrastructure<TDbContext>(tenantMode, databaseName);
    }
}
