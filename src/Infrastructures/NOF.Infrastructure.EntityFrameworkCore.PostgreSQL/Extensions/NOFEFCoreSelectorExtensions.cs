using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace NOF;

public static partial class __NOF_Infrastructure_EntityFrameworkCore_PostgreSQL_Extensions__
{
    extension(INOFEFCoreSelector selector)
    {
        public INOFAppBuilder UsePostgreSQL(string connectStringName = "postgres")
        {
            selector.Builder.StartupEventChannel.Subscribe<DbContextConfigurating>(e =>
            {
                var connectString = selector.Builder.Configuration.GetConnectionString(connectStringName);
                e.Options.UseNpgsql(connectString);
            });
            selector.Builder.UsedPostgreSQL = true;
            return selector.Builder;
        }
    }
}
