namespace NOF.Infrastructure.Core;

public static partial class NOFInfrastructureCoreExtensions
{
    extension(string str)
    {
        public static string GetSectionNameFromOptions(string optionsName)
        {
            const string options = "options";
            if (optionsName.EndsWith(options, StringComparison.OrdinalIgnoreCase))
            {
                optionsName = optionsName[..^options.Length];
            }

            return optionsName;
        }

        public static string GetSectionNameFromOptions<TOptions>() where TOptions : class
            => string.GetSectionNameFromOptions(typeof(TOptions).Name);
    }
}
