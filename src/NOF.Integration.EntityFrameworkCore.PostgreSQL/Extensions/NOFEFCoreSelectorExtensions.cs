using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace NOF;

public static partial class __NOF_Infrastructure__EntityFrameworkCore__PostgreSQL__
{
    extension(INOFEFCoreSelector selector)
    {
        public INOFApp UsePostgreSQL(string connectStringName = "postgres")
        {
            EventDispatcher.Subscribe<DbContextConfigurating>(e =>
            {
                var connectString = selector.App.Unwrap().Configuration.GetConnectionString(connectStringName);
                e.Options.UseNpgsql(connectString);
            });
            selector.App.Metadata.UsePostgreSQL = true;
            return selector.App;
        }
    }
}
