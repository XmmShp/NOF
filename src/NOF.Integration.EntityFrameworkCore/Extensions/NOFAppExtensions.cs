using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace NOF;

public record DbContextConfigurating(DbContextOptionsBuilder Options);

public static partial class __NOF_Infrastructure__EntityFrameworkCore__
{
    extension(INOFApp app)
    {
        public INOFEFCoreSelector AddEFCore<TDbContext>()
            where TDbContext : NOFDbContext
        {
            app.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            app.Services.AddDbContext<TDbContext>(options =>
            {
                EventDispatcher.Publish(new DbContextConfigurating(options));
            });
            app.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());
            app.Metadata.UseEntityFrameworkCore = true;
            app.Metadata.DbContextType = typeof(TDbContext);
            return new NOFEFCoreSelector(app);
        }
    }
}