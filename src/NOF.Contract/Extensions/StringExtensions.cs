namespace NOF;

/// <summary>
/// Extension methods for the NOF.Contract layer.
/// </summary>
public static partial class __NOF_Contract_Extensions__
{
    extension(string str)
    {
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

        /// <summary>
        /// Matches a string against a glob pattern containing '*' wildcards.
        /// Supports patterns like "a.*All.c", "*.log", "prefix*suffix", etc.
        /// The '*' matches any sequence of characters (including empty).
        /// </summary>
        public bool MatchWildcard(string pattern, StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return string.IsNullOrEmpty(str);
            }

            var segments = pattern.Split('*');

            if (segments.Length == 1)
            {
                return pattern.Equals(str, comparison);
            }

            if (!str.StartsWith(segments[0], comparison))
            {
                return false;
            }

            var last = segments[^1];
            if (!str.EndsWith(last, comparison))
            {
                return false;
            }

            var start = segments[0].Length;
            var end = str.Length - last.Length;
            if (start > end)
            {
                return false;
            }

            var middle = str.Substring(start, end - start);

            for (var i = 1; i < segments.Length - 1; i++)
            {
                var seg = segments[i];
                if (string.IsNullOrEmpty(seg))
                    continue;

                var index = middle.IndexOf(seg, comparison);
                if (index == -1)
                {
                    return false;
                }
                middle = middle[(index + seg.Length)..];
            }

            return true;
        }
    }
}
