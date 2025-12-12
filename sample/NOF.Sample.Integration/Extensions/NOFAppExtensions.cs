namespace NOF.Sample;

public static class __NOF_Sample__
{
    extension(INOFApp app)
    {
        public INOFApp AddConfigurationFromCenter<TClient>()
            where TClient : class
            => app.AddConfigurationFromCenter(string.GetSystemNameFromClient<TClient>());

        public INOFApp AddConfigurationFromCenter(string systemName)
        {
            return app.AddRegistrationConfigurator(new ConfigurationCenterConfigurator(systemName));
        }
    }
}