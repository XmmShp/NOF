namespace NOF;

public static partial class __NOF_Contract_Extensions__
{
    extension(string str)
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
