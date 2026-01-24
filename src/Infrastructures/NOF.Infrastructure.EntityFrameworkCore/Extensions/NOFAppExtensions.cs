using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace NOF;

public record DbContextConfigurating(IServiceProvider ServiceProvider, DbContextOptionsBuilder Options);

public static partial class __NOF_Infrastructure_EntityFrameworkCore_Extensions__
{
    extension(INOFAppBuilder builder)
    {
        public INOFEFCoreSelector AddEFCore<TDbContext>() where TDbContext : NOFDbContext
        {
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<ITransactionManager, EFCoreTransactionManager>();
            builder.Services.AddScoped<IInboxMessageRepository, InboxMessageRepository>();
            builder.Services.AddScoped<IStateMachineContextRepository, StateMachineContextRepository>();
            builder.Services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
            builder.Services.AddHostedService<InboxCleanupBackgroundService>();
            builder.Services.AddHostedService<OutboxCleanupBackgroundService>();

            // Register DbContext factory - actual configuration will be done by specific database providers
            builder.Services.AddScoped<TDbContext>(sp =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
                ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(new NOFDbContextOptionsExtension(builder.StartupEventChannel));
                builder.StartupEventChannel.Publish(new DbContextConfigurating(sp, optionsBuilder));

                var dbContext = ActivatorUtilities.CreateInstance<TDbContext>(sp, optionsBuilder.Options);

                return dbContext;
            });
            builder.Services.AddScoped<NOFDbContext>(sp => sp.GetRequiredService<TDbContext>());
            builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());

            builder.Services.AddScoped<NOFPublicDbContext>(sp =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<NOFPublicDbContext>();
                ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(new NOFDbContextOptionsExtension(builder.StartupEventChannel));
                builder.StartupEventChannel.Publish(new DbContextConfigurating(sp, optionsBuilder));

                var dbContext = ActivatorUtilities.CreateInstance<NOFPublicDbContext>(sp, optionsBuilder.Options);

                return dbContext;
            });

            return new NOFEFCoreSelector(builder);
        }
    }
}
