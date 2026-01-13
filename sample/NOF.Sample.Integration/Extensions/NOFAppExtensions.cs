namespace NOF.Sample;

public static class __NOF_Sample__
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddConfigurationFromCenter<TClient>()
            where TClient : class
            => builder.AddConfigurationFromCenter(string.GetSystemNameFromClient<TClient>());

        public INOFAppBuilder AddConfigurationFromCenter(string systemName)
        {
            return builder.AddRegistrationStep(new ConfigurationCenterRegistrationStep(systemName));
        }
    }
}