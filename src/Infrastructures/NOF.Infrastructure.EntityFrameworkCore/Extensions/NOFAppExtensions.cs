using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace NOF;

public record DbContextConfigurating(IServiceProvider ServiceProvider, string TenantId, DbContextOptionsBuilder Options);
public record PublicDbContextConfigurating(IServiceProvider ServiceProvider, DbContextOptionsBuilder Options);

public static partial class __NOF_Infrastructure_EntityFrameworkCore_Extensions__
{
    private const string AutoMigrateTenantDatabases = "NOF.Infrastructure.EntityFrameworkCore:AutoMigrateTenantDatabases";

    extension(INOFAppBuilder builder)
    {
        public bool AutoMigrateTenantDatabases
        {
            get => builder.Properties.GetOrDefault<bool>(AutoMigrateTenantDatabases);
            set => builder.Properties[AutoMigrateTenantDatabases] = value;
        }
    }

    extension(INOFAppBuilder builder)
    {
        public INOFEFCoreSelector AddEFCore<TTenantDbContext, TPublicDbContext>()
            where TTenantDbContext : NOFDbContext
            where TPublicDbContext : NOFPublicDbContext
        {
            #region Tenant Related Services
            builder.Services.AddScoped<IInboxMessageRepository, EFCoreInboxMessageRepository>();
            builder.Services.AddScoped<IStateMachineContextRepository, EFCoreStateMachineContextRepository>();
            builder.Services.AddScoped<IOutboxMessageRepository, EFCoreOutboxMessageRepository>();

            builder.Services.AddScoped<TTenantDbContext>(sp =>
            {
                var factory = sp.GetRequiredService<INOFDbContextFactory<TTenantDbContext>>();
                var tenantContext = sp.GetRequiredService<ITenantContext>();
                return factory.CreateDbContext(tenantContext.CurrentTenantId);
            });
            builder.Services.AddScoped<IUnitOfWork, EFCoreUnitOfWork>();
            builder.Services.AddScoped<ITransactionManager, EFCoreTransactionManager>();
            builder.Services.TryAddScoped<NOFDbContext>(sp => sp.GetRequiredService<TTenantDbContext>());
            builder.Services.TryAddScoped<DbContext>(sp => sp.GetRequiredService<TTenantDbContext>());
            #endregion

            #region Public Related Services
            builder.Services.AddScoped<ITenantRepository, EFCoreTenantRepository>();
            builder.Services.AddScoped<TPublicDbContext>(sp =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<TPublicDbContext>();
                ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(new NOFDbContextOptionsExtension(builder.StartupEventChannel));
                builder.StartupEventChannel.Publish(new PublicDbContextConfigurating(sp, optionsBuilder));

                var dbContext = ActivatorUtilities.CreateInstance<TPublicDbContext>(sp, optionsBuilder.Options);

                return dbContext;
            });
            builder.Services.TryAddScoped<NOFPublicDbContext>(sp => sp.GetRequiredService<TPublicDbContext>());
            #endregion

            #region Infrastructure Services
            builder.Services.AddScoped<INOFDbContextFactory<TTenantDbContext>>(sp => new NOFDbContextFactory<TTenantDbContext>(
                sp,
                sp.GetRequiredService<ITenantContext>(),
                builder.StartupEventChannel,
                builder.AutoMigrateTenantDatabases,
                sp.GetRequiredService<ILogger<NOFDbContextFactory<TTenantDbContext>>>()));
            builder.Services.AddScoped<IDbContextFactory<TTenantDbContext>>(sp => sp.GetRequiredService<INOFDbContextFactory<TTenantDbContext>>());

            builder.Services.AddScoped<IDataAccessContext, EFCoreDataAccessContext<TTenantDbContext>>();
            builder.Services.AddScoped<IPublicDataAccessContextFactory, EFCoreDataAccessContextFactory<TPublicDbContext>>();
            builder.Services.AddScoped<IDataAccessContextFactory, EFCoreDataAccessContextFactory<TTenantDbContext>>();

            builder.Services.AddHostedService<InboxCleanupBackgroundService>();
            builder.Services.AddHostedService<OutboxCleanupBackgroundService>();

            #endregion

            return new NOFEFCoreSelector(builder);
        }
    }
}
