using Microsoft.Extensions.DependencyInjection;

namespace NOF;

public static partial class NOFInfrastructureEntityFrameworkCoreExtensions
{
    extension(IEFCoreSelector selector)
    {
        public IEFCoreSelector AutoMigrate()
        {
            selector.Builder.Services.Configure<DbContextFactoryOptions>(options =>
            {
                options.AutoMigrate = true;
            });
            return selector;
        }
    }
}
