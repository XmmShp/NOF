using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Domain;
using NOF.Hosting;

namespace NOF.Infrastructure.EntityFrameworkCore;

public static class NOFInfrastructureEntityFrameworkCoreExtensions
{
    extension(INOFAppBuilder builder)
    {
        public EFCoreSelector AddEFCore<TDbContext>()
            where TDbContext : NOFDbContext
        {
            #region Common Services
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Scoped(typeof(IRepository<>), typeof(EFCoreRepository<>)));
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Scoped(typeof(IRepository<,>), typeof(EFCoreRepository<,>)));
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Scoped(typeof(IRepository<,,>), typeof(EFCoreRepository<,,>)));
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Scoped(typeof(IRepository<,,,>), typeof(EFCoreRepository<,,,>)));
            builder.Services.ReplaceOrAddScoped<IOutboxMessageRepository, EFCoreOutboxMessageRepository>();
            builder.Services.ReplaceOrAddScoped<IUnitOfWork, EFCoreUnitOfWork>();
            builder.Services.ReplaceOrAddScoped<ITransactionManager, EFCoreTransactionManager>();
            #endregion

            #region DbContext Services
            builder.Services.ReplaceOrAddScoped(sp =>
            {
                var factory = sp.GetRequiredService<INOFDbContextFactory<TDbContext>>();
                var executionContext = sp.GetRequiredService<IExecutionContext>();
                return factory.CreateDbContext(executionContext.TenantId);
            });
            builder.Services.ReplaceOrAddScoped<NOFDbContext>(sp => sp.GetRequiredService<TDbContext>());
            builder.Services.ReplaceOrAddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());
            #endregion

            #region Infrastructure Services
            builder.Services.ReplaceOrAddScoped<INOFDbContextFactory<TDbContext>>(sp => new NOFDbContextFactory<TDbContext>(
                sp,
                sp.GetRequiredService<IExecutionContext>(),
                sp.GetRequiredService<IOptions<TenantOptions>>(),
                sp.GetRequiredService<IDbContextConfigurator>(),
                sp.GetRequiredService<IOptions<DbContextFactoryOptions>>(),
                sp.GetRequiredService<ILogger<NOFDbContextFactory<TDbContext>>>()));

            builder.Services.ReplaceOrAddScoped<IDbContextFactory<TDbContext>>(sp => sp.GetRequiredService<INOFDbContextFactory<TDbContext>>());

            builder.Services.AddHostedService<InboxCleanupBackgroundService>();
            builder.Services.AddHostedService<OutboxCleanupBackgroundService>();

            #endregion

            return new EFCoreSelector(builder);
        }
    }
}
