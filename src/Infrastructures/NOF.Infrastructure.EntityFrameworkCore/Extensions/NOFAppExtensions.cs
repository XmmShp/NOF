using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NOF;

public record DbContextConfigurating(IServiceProvider ServiceProvider, DbContextOptionsBuilder Options);

public static partial class __NOF_Infrastructure_EntityFrameworkCore_Extensions__
{
    extension(INOFAppBuilder builder)
    {
        public INOFEFCoreSelector AddEFCore()
            => builder.AddEFCore<NOFDbContext, NOFPublicDbContext>();

        public INOFEFCoreSelector AddEFCore<TTenantDbContext>() where TTenantDbContext : NOFDbContext
            => builder.AddEFCore<TTenantDbContext, NOFPublicDbContext>();

        public INOFEFCoreSelector AddEFCore<TTenantDbContext, TPublicDbContext>()
            where TTenantDbContext : NOFDbContext
            where TPublicDbContext : NOFPublicDbContext
        {
            builder.Services.AddScoped<IUnitOfWork, EFCoreUnitOfWork>();
            builder.Services.AddScoped<ITransactionManager, EFCoreTransactionManager>();
            builder.Services.AddScoped<IInboxMessageRepository, EFCoreInboxMessageRepository>();
            builder.Services.AddScoped<IStateMachineContextRepository, EFCoreStateMachineContextRepository>();
            builder.Services.AddScoped<IOutboxMessageRepository, EFCoreOutboxMessageRepository>();
            builder.Services.AddHostedService<InboxCleanupBackgroundService>();
            builder.Services.AddHostedService<OutboxCleanupBackgroundService>();

            // Register custom tenant DbContext
            builder.Services.AddScoped<TTenantDbContext>(sp =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<TTenantDbContext>();
                ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(new NOFDbContextOptionsExtension(builder.StartupEventChannel));
                builder.StartupEventChannel.Publish(new DbContextConfigurating(sp, optionsBuilder));

                var dbContext = ActivatorUtilities.CreateInstance<TTenantDbContext>(sp, optionsBuilder.Options);

                return dbContext;
            });
            builder.Services.TryAddScoped<NOFDbContext>(sp => sp.GetRequiredService<TTenantDbContext>());
            builder.Services.TryAddScoped<DbContext>(sp => sp.GetRequiredService<TTenantDbContext>());

            // Register custom public DbContext
            builder.Services.AddScoped<TPublicDbContext>(sp =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<TPublicDbContext>();
                ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(new NOFDbContextOptionsExtension(builder.StartupEventChannel));
                builder.StartupEventChannel.Publish(new DbContextConfigurating(sp, optionsBuilder));

                var dbContext = ActivatorUtilities.CreateInstance<TPublicDbContext>(sp, optionsBuilder.Options);

                return dbContext;
            });
            builder.Services.TryAddScoped<NOFPublicDbContext>(sp => sp.GetRequiredService<TPublicDbContext>());

            return new NOFEFCoreSelector(builder);
        }
    }
}
