namespace NOF;

public static partial class __NOF_Infrastructure__EntityFrameworkCore__PostgreSQL__
{
    private const string UsePostgreSQL_ = "NOF.Infrastructure.EntityFrameworkCore.PostgreSQL:UsePostgreSQL";
    extension(INOFAppMetadata metadata)
    {
        public bool UsePostgreSQL
        {
            get => metadata.GetOrAdd(UsePostgreSQL_, _ => false);
            set => metadata.Set(UsePostgreSQL_, value);
        }
    }
}
