using NOF.Contract;
using NOF.Infrastructure.Abstraction;

namespace NOF.Sample;

public static class NOFSample
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
