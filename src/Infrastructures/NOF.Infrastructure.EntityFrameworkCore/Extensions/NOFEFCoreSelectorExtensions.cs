using Microsoft.Extensions.DependencyInjection;

namespace NOF;

public static partial class __NOF_Infrastructure_EntityFrameworkCore_Extensions__
{
    extension(INOFEFCoreSelector selector)
    {
        public INOFEFCoreSelector AutoMigrate()
        {
            selector.Builder.Services.Configure<DbContextFactoryOptions>(options =>
            {
                options.AutoMigrate = true;
            });
            return selector;
        }
    }
}
