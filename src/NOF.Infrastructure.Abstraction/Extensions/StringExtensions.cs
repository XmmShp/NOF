namespace NOF.Infrastructure.Core;

public static class NOFInfrastructureCoreExtensions
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

        /// <summary>Extracts the system name from a client type name by removing the "Client" suffix.</summary>
        /// <param name="clientName">The client type name.</param>
        /// <returns>The system name.</returns>
        public static string GetSystemNameFromClient(string clientName)
        {
            const string client = "client";
            if (clientName.EndsWith(client, StringComparison.OrdinalIgnoreCase))
            {
                clientName = clientName[..^client.Length];
            }
            return clientName;
        }

        /// <summary>Extracts the system name from a client type by removing the "Client" suffix.</summary>
        /// <typeparam name="TClient">The client type.</typeparam>
        /// <returns>The system name.</returns>
        public static string GetSystemNameFromClient<TClient>() where TClient : class
            => string.GetSystemNameFromClient(typeof(TClient).Name);
    }
}
