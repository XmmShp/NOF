using Microsoft.Extensions.Hosting;

namespace NOF;

public static partial class __NOF_Infrastructure__EntityFrameworkCore__
{
    extension(INOFEFCoreSelector selector)
    {
        public INOFEFCoreSelector AutoMigrate<THostApplication>(INOFAppBuilder<THostApplication> originBuilder) where THostApplication : class, IHost
        {
            originBuilder.AddApplicationConfig(new AutoMigrateConfig<THostApplication>());
            return selector;
        }
    }
}
