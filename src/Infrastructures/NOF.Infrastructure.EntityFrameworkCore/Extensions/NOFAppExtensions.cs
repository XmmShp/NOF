using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Infrastructure.Core;

namespace NOF.Infrastructure.EntityFrameworkCore;

public static partial class NOFInfrastructureEntityFrameworkCoreExtensions
{
    extension(INOFAppBuilder builder)
    {
        public EFCoreSelector AddEFCore<TDbContext>()
            where TDbContext : NOFDbContext
        {
            #region Common Services
            builder.Services.AddScoped<IInboxMessageRepository, EFCoreInboxMessageRepository>();
            builder.Services.AddScoped<IStateMachineContextRepository, EFCoreStateMachineContextRepository>();
            builder.Services.AddScoped<IOutboxMessageRepository, EFCoreOutboxMessageRepository>();
            builder.Services.AddScoped<ITenantRepository, EFCoreTenantRepository>();
            builder.Services.AddScoped<IUnitOfWork, EFCoreUnitOfWork>();
            builder.Services.AddScoped<ITransactionManager, EFCoreTransactionManager>();
            #endregion

            #region DbContext Services
            builder.Services.AddScoped<TDbContext>(sp =>
            {
                var factory = sp.GetRequiredService<INOFDbContextFactory<TDbContext>>();
                var invocationContext = sp.GetRequiredService<IInvocationContext>();
                return factory.CreateDbContext(invocationContext.TenantId);
            });
            builder.Services.TryAddScoped<NOFDbContext>(sp => sp.GetRequiredService<TDbContext>());
            builder.Services.TryAddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());
            #endregion

            #region Infrastructure Services
            builder.Services.AddOptionsInConfiguration<DbContextFactoryOptions>();

            builder.Services.AddScoped<INOFDbContextFactory<TDbContext>>(sp => new NOFDbContextFactory<TDbContext>(
                sp,
                sp.GetRequiredService<IInvocationContext>(),
                sp.GetRequiredService<IDbContextConfigurator>(),
                sp.GetRequiredService<IOptions<DbContextFactoryOptions>>(),
                sp.GetRequiredService<ILogger<NOFDbContextFactory<TDbContext>>>()));

            builder.Services.AddScoped<IDbContextFactory<TDbContext>>(sp => sp.GetRequiredService<INOFDbContextFactory<TDbContext>>());

            builder.Services.AddHostedService<InboxCleanupBackgroundService>();
            builder.Services.AddHostedService<OutboxCleanupBackgroundService>();

            #endregion

            return new EFCoreSelector(builder);
        }
    }
}
