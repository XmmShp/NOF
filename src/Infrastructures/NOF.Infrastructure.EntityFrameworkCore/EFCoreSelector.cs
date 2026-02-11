using Microsoft.Extensions.DependencyInjection;

namespace NOF;

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
