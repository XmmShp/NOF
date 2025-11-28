using Microsoft.Extensions.Configuration;

namespace NOF;

public static class ConfigurationManagerExtensions
{
    extension(ConfigurationManager manager)
    {
        public TOptions? GetOptions<TOptions>() where TOptions : class
        {
            var sectionName = string.GetSectionNameFromOptions<TOptions>();
            return manager.GetSection(sectionName).Get<TOptions>();
        }

        public TOptions GetRequiredOptions<TOptions>() where TOptions : class
            => manager.GetOptions<TOptions>() ?? throw new InvalidOperationException();
    }
}
