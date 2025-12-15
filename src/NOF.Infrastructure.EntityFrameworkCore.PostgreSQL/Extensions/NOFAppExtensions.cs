namespace NOF;

public static partial class __NOF_Infrastructure__EntityFrameworkCore__PostgreSQL__
{
    private const string UsedPostgreSQL = "NOF.Infrastructure.EntityFrameworkCore.PostgreSQL:UsedPostgreSQL";
    extension(INOFAppBuilder builder)
    {
        public bool UsedPostgreSQL
        {
            get => builder.Properties.GetOrDefault<bool>(UsedPostgreSQL);
            set => builder.Properties[UsedPostgreSQL] = value;
        }
    }
}