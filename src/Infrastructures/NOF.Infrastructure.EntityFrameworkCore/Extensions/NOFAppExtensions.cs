using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace NOF;

public record DbContextConfigurating(DbContextOptionsBuilder Options);

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
            builder.Services.AddScoped<IStateMachineContextRepository, StateMachineContextRepository>();
            builder.Services.AddScoped<ITransactionalMessageRepository, TransactionalMessageRepository>();
            builder.Services.AddScoped<ITransactionalMessageCollector, TransactionalMessageCollector>();
            builder.Services.AddHostedService<OutboxCleanupService>();
            builder.Services.AddDbContext<TDbContext>(options =>
            {
                ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(new NOFDbContextOptionsExtension(builder.StartupEventChannel));
                builder.StartupEventChannel.Publish(new DbContextConfigurating(options));
            });
            builder.Services.AddScoped<NOFDbContext>(sp => sp.GetRequiredService<TDbContext>());
            builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());
            builder.UseEntityFrameworkCore = true;
            builder.DbContextType = typeof(TDbContext);
            return new NOFEFCoreSelector(builder);
        }
    }
}
