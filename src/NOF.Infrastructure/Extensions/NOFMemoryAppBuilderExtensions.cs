using NOF.Abstraction;
using NOF.Hosting;

namespace NOF.Infrastructure;

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

            builder.Services.ReplaceOrAddScoped<IEventPublisher, InMemoryEventPublisher>();

            builder.Services.ReplaceOrAddSingleton<ICommandRider, MemoryCommandRider>();
            builder.Services.ReplaceOrAddSingleton<INotificationRider, MemoryNotificationRider>();

            _ = tenantMode switch
            {
                TenantMode.SharedDatabase => builder.UseSharedDatabaseTenancy(),
                TenantMode.DatabasePerTenant => builder.UseDatabasePerTenant(),
                _ => builder.UseSingleTenant()
            };

            builder.AddEFCore<TDbContext>()
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
