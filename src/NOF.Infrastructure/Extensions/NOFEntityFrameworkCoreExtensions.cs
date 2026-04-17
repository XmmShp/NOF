using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

public static class NOFInfrastructureEntityFrameworkCoreExtensions
{
    extension(INOFAppBuilder builder)
    {
        public EFCoreSelector AddEFCore()
            => builder.AddEFCore<NOFDbContext>();

        public EFCoreSelector AddEFCore<TDbContext>()
            where TDbContext : NOFDbContext
        {
            #region DbContext Services
            builder.Services.ReplaceOrAddScoped(sp =>
            {
                var factory = sp.GetRequiredService<INOFDbContextFactory<TDbContext>>();
                var executionContext = sp.GetRequiredService<IExecutionContext>();
                return factory.CreateDbContext(executionContext.TenantId);
            });
            if (typeof(TDbContext) != typeof(NOFDbContext))
            {
                builder.Services.ReplaceOrAddScoped<NOFDbContext>(sp => sp.GetRequiredService<TDbContext>());
            }
            builder.Services.ReplaceOrAddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());
            #endregion

            #region Infrastructure Services
            builder.Services.ReplaceOrAddScoped<INOFDbContextFactory<TDbContext>>(sp => new NOFDbContextFactory<TDbContext>(
                sp,
                sp.GetRequiredService<IExecutionContext>(),
                sp.GetRequiredService<IOptions<TenantOptions>>(),
                sp.GetRequiredService<DbContextOptionsConfiguration>(),
                sp.GetRequiredService<IOptions<DbContextFactoryOptions>>(),
                sp.GetRequiredService<ILogger<NOFDbContextFactory<TDbContext>>>()));

            builder.Services.ReplaceOrAddScoped<IDbContextFactory<TDbContext>>(sp => sp.GetRequiredService<INOFDbContextFactory<TDbContext>>());

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, InboxCleanupBackgroundService>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxCleanupBackgroundService>());

            #endregion

            return new EFCoreSelector(builder);
        }
    }
}
