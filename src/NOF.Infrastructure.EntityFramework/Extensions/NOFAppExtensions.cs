namespace NOF;

public static partial class __NOF_Infrastructure__EntityFrameworkCore__PostgreSQL__
{
    extension(INOFApp app)
    {
        public INOFEFCoreApp<TDbContext> AddEFCore<TDbContext>(bool autoMigration = false)
            where TDbContext : NOFDbContext
        {
            app.AddRegistrationConfigurator<EFCoreConfigurator>();
            if (autoMigration)
            {
                app.AddStartupConfigurator<AutoMigrationConfigurator>();
            }
            return new NOFEFCoreApp<TDbContext>(app);
        }
    }
}