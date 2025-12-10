namespace NOF;

public static partial class __NOF_Infrastructure__EntityFrameworkCore__
{
    extension(INOFApp app)
    {
        public INOFEFCoreApp<TDbContext> AddEFCore<TDbContext>()
            where TDbContext : NOFDbContext
        {
            app.AddRegistrationConfigurator<EFCoreConfigurator>();
            return new NOFEFCoreApp<TDbContext>(app);
        }
    }
}