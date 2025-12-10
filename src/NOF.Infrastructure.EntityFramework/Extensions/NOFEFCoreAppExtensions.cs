namespace NOF;

public static partial class __NOF_Infrastructure__EntityFrameworkCore__
{
    extension<TDbContext>(INOFEFCoreApp<TDbContext> app)
        where TDbContext : NOFDbContext
    {
        public INOFEFCoreApp<TDbContext> AutoMigrate()
        {
            app.App.AddStartupConfigurator<AutoMigrationConfigurator>();
            return app;
        }
    }
}
