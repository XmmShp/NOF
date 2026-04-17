using Microsoft.Extensions.DependencyInjection;
using NOF.Hosting;

namespace NOF.Infrastructure;

public readonly struct EFCoreSelector
{
    public INOFAppBuilder Builder { get; }

    public EFCoreSelector(INOFAppBuilder builder)
    {
        Builder = builder;
    }

    public EFCoreSelector AutoMigrate()
    {
        Builder.Services.Configure<DbContextFactoryOptions>(options =>
        {
            options.AutoMigrate = true;
        });
        return this;
    }
}
