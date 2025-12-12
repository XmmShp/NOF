namespace NOF;

public static partial class __NOF_Infrastructure__EntityFrameworkCore__
{
    extension(INOFEFCoreSelector selector)
    {
        public INOFEFCoreSelector AutoMigrate()
        {
            selector.App.AddStartupConfigurator<AutoMigrateConfigurator>();
            return selector;
        }
    }
}
