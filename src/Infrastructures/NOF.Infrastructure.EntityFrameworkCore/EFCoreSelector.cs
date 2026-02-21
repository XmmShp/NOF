using Microsoft.Extensions.DependencyInjection;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.EntityFrameworkCore;

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
