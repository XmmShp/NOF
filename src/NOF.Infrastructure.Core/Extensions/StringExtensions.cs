namespace NOF;

public static partial class __NOF_Infrastructure_Core_Extensions__
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
