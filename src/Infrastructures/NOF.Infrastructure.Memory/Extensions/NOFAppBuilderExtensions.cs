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
            builder.Services.ReplaceOrAddCacheService<MemoryCacheService>();

            builder.Services.ReplaceOrAddScoped<IEventPublisher, EventPublisher>();

            builder.Services.ReplaceOrAddSingleton<ICommandRider, MemoryCommandRider>();
            builder.Services.ReplaceOrAddSingleton<INotificationRider, MemoryNotificationRider>();

            return builder;
        }

        public INOFAppBuilder AddMemoryInfrastructureWithSqlite<TDbContext>(TenantMode tenantMode = TenantMode.SingleTenant, string databaseName = "nof-sqlite-memory")
            where TDbContext : NOFDbContext
        {
            builder.AddMemoryInfrastructure();

            var selector = builder.AddEFCore<TDbContext>();
            selector = tenantMode switch
            {
                TenantMode.SharedDatabase => selector.UseSharedDatabaseTenancy(),
                TenantMode.DatabasePerTenant => selector.UseDatabasePerTenant(),
                _ => selector.UseSingleTenant()
            };

            selector.UseSqliteInMemory(databaseName);
            return builder;
        }
    }
}
