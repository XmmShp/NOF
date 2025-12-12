namespace NOF;

public static partial class __NOF_Contract_Extensions__
{
    extension(string)
    {
        public static string GetSystemNameFromClient(string clientName)
        {
            const string client = "client";
            if (clientName.EndsWith(client, StringComparison.OrdinalIgnoreCase))
            {
                clientName = clientName[..^client.Length];
            }
            return clientName;
        }

        public static string GetSystemNameFromClient<TClient>() where TClient : class
            => string.GetSystemNameFromClient(typeof(TClient).Name);

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
