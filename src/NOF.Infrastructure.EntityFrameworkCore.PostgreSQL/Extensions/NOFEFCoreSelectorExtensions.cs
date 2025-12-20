using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace NOF;

public static partial class __NOF_Infrastructure_EntityFrameworkCore_PostgreSQL_Extensions__
{
    extension<THostApplication>(INOFEFCoreSelector<THostApplication> selector)
        where THostApplication : class, IHost
    {
        public INOFAppBuilder<THostApplication> UsePostgreSQL(string connectStringName = "postgres")
        {
            selector.Builder.EventDispatcher.Subscribe<DbContextConfigurating>(e =>
            {
                var connectString = selector.Builder.Configuration.GetConnectionString(connectStringName);
                e.Options.UseNpgsql(connectString);
            });
            selector.Builder.UsedPostgreSQL = true;
            return selector.Builder;
        }
    }
}
