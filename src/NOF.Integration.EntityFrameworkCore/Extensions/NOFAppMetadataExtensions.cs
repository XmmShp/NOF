namespace NOF;

public static partial class __NOF_Infrastructure__EntityFrameworkCore__
{
    private const string UseEntityFrameworkCore = "NOF.Infrastructure.EntityFrameworkCore:UseEntityFrameworkCore";
    private const string DbContextType = "NOF.Infrastructure.EntityFrameworkCore:DbContextType";
    extension(INOFAppMetadata metadata)
    {
        public bool UseEntityFrameworkCore
        {
            get => metadata.GetOrAdd(UseEntityFrameworkCore, _ => false);
            set => metadata.Set(UseEntityFrameworkCore, value);
        }

        public Type DbContextType
        {
            get => metadata.GetOrAdd(DbContextType, _ => typeof(NOFDbContext));
            set => metadata.Set(DbContextType, value);
        }
    }
}
