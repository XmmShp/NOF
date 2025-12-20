using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace NOF;

public record DbContextConfigurating(DbContextOptionsBuilder Options);

public static partial class __NOF_Infrastructure__EntityFrameworkCore__
{
    private const string UseEntityFrameworkCore = "NOF.Infrastructure.EntityFrameworkCore:UseEntityFrameworkCore";
    private const string DbContextType = "NOF.Infrastructure.EntityFrameworkCore:DbContextType";
    extension(INOFAppBuilder builder)
    {
        public bool UseEntityFrameworkCore
        {
            get => builder.Properties.GetOrDefault<bool>(UseEntityFrameworkCore);
            set => builder.Properties[UseEntityFrameworkCore] = value;
        }

        public Type? DbContextType
        {
            get => builder.Properties.GetOrDefault<Type>(DbContextType);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                builder.Properties[DbContextType] = value;
            }
        }
    }
    extension<THostApplication>(INOFAppBuilder<THostApplication> builder) where THostApplication : class, IHost
    {
        public INOFEFCoreDbContextSelector<THostApplication> AddEFCore()
        {
            return new NOFEFCoreDbContextSelector<THostApplication>(builder);
        }
    }
}