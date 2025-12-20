using Microsoft.Extensions.Hosting;

namespace NOF;

public static partial class __NOF_Infrastructure__EntityFrameworkCore__
{
    extension<THostApplication>(INOFEFCoreSelector<THostApplication> selector)
        where THostApplication : class, IHost
    {
        public INOFEFCoreSelector<THostApplication> AutoMigrate()
        {
            selector.Builder.AddApplicationConfig(new AutoMigrateConfig<THostApplication>());
            return selector;
        }
    }
}
