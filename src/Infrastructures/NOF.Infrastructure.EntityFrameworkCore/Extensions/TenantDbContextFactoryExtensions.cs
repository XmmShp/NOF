using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// Migration helpers for explicitly selected tenant databases.
/// </summary>
public static class TenantDbContextFactoryExtensions
{
    extension<TDbContext>(ITenantDbContextFactory<TDbContext> dbContextFactory) where TDbContext : NOFDbContext
    {
        /// <summary>
        /// Applies all pending migrations to the database resolved for <paramref name="tenantId"/>.
        /// </summary>
        /// <typeparam name="TDbContext">The concrete NOF database context type.</typeparam>
        /// <param name="dbContextFactory">The tenant-aware database context factory.</param>
        /// <param name="tenantId">The tenant whose database should be migrated.</param>
        /// <param name="cancellationToken">A token used to cancel the migration.</param>
        public async Task MigrateAsync(
            string tenantId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbContextFactory);
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

            await using var dbContext = dbContextFactory.CreateDbContext(tenantId);
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
    }
}
