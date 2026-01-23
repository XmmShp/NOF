using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace NOF;

public record DbContextConfigurating(IServiceProvider ServiceProvider, DbContextOptionsBuilder Options);

public static partial class __NOF_Infrastructure_EntityFrameworkCore_Extensions__
{
    private const string UseEntityFrameworkCore = "NOF.Infrastructure.EntityFrameworkCore:UseEntityFrameworkCore";
    private const string DbContextType = "NOF.Infrastructure.EntityFrameworkCore:DbContextType";

    extension(INOFAppBuilder builder)
    {
        public bool UseEntityFrameworkCore
        {
            get => builder.Properties.GetOrDefault<bool>(UseEntityFrameworkCore);
            set => builder.Properties[UseEntityFrameworkCore] = value;
        }

        public Type? DbContextType
        {
            get => builder.Properties.GetOrDefault<Type>(DbContextType);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                builder.Properties[DbContextType] = value;
            }
        }
    }

    extension(INOFAppBuilder builder)
    {
        public INOFEFCoreSelector AddEFCore<TDbContext>() where TDbContext : NOFDbContext
        {
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<ITransactionManager, TransactionManager>();
            builder.Services.AddScoped<IInboxMessageRepository, InboxMessageRepository>();
            builder.Services.AddScoped<IStateMachineContextRepository, StateMachineContextRepository>();
            builder.Services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
            builder.Services.AddScoped<IOutboxMessageCollector, OutboxMessageCollector>();
            builder.Services.AddHostedService<InboxCleanupService>();
            builder.Services.AddHostedService<OutboxCleanupService>();

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
            builder.UseEntityFrameworkCore = true;
            builder.DbContextType = typeof(TDbContext);
            return new NOFEFCoreSelector(builder);
        }
    }
}
