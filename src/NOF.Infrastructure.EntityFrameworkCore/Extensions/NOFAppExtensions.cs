using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace NOF;

public record DbContextConfigurating(DbContextOptionsBuilder Options);

public static partial class __NOF_Infrastructure__EntityFrameworkCore__
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

        public INOFEFCoreSelector AddEFCore<TDbContext>()
            where TDbContext : NOFDbContext
        {
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<IStateMachineContextRepository, StateMachineContextRepository<TDbContext>>();
            builder.Services.AddDbContext<TDbContext>(options =>
            {
                ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(new NOFDbContextOptionsExtension(builder.EventDispatcher));
                builder.EventDispatcher.Publish(new DbContextConfigurating(options));
            });
            builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());
            builder.UseEntityFrameworkCore = true;
            builder.DbContextType = typeof(TDbContext);
            return new NOFEFCoreSelector(builder);
        }
    }
}