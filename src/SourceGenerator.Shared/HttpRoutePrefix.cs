using System;

namespace NOF.Internal;

internal static class HttpRoutePrefix
{
    public static string Normalize(string routePrefix)
    {
        if (!TryNormalize(routePrefix, out var normalizedRoutePrefix, out var error))
        {
            throw new ArgumentException(error, nameof(routePrefix));
        }

        return normalizedRoutePrefix;
    }

    public static bool TryNormalize(
        string? routePrefix,
        out string normalizedRoutePrefix,
        out string? error)
    {
        normalizedRoutePrefix = string.Empty;

        if (string.IsNullOrEmpty(routePrefix))
        {
            error = "Route prefix must not be empty. Omit it or use '/' for the root route.";
            return false;
        }

        foreach (var character in routePrefix!)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                error = "Route prefix must not contain whitespace or control characters.";
                return false;
            }
        }

        if (routePrefix.IndexOf('?') >= 0 || routePrefix.IndexOf('#') >= 0)
        {
            error = "Route prefix must not contain a query string or fragment.";
            return false;
        }

        if (routePrefix.IndexOf('{') >= 0 || routePrefix.IndexOf('}') >= 0)
        {
            error = "Route prefix must not contain route parameters.";
            return false;
        }

        if (routePrefix.IndexOf('\\') >= 0)
        {
            error = "Route prefix must use forward slashes.";
            return false;
        }

        if (routePrefix.IndexOf('%') >= 0)
        {
            error = "Route prefix must use unescaped path segments.";
            return false;
        }

        if (!routePrefix.StartsWith("/", StringComparison.Ordinal)
            && Uri.TryCreate(routePrefix, UriKind.Absolute, out _))
        {
            error = "Route prefix must be an application-relative path, not an absolute URI.";
            return false;
        }

        var candidate = routePrefix.StartsWith("/", StringComparison.Ordinal)
            ? routePrefix
            : "/" + routePrefix;

        if (candidate.IndexOf("//", StringComparison.Ordinal) >= 0)
        {
            error = "Route prefix must not contain empty path segments ('//').";
            return false;
        }

        if (candidate.Length > 1 && candidate.EndsWith("/", StringComparison.Ordinal))
        {
            candidate = candidate.Substring(0, candidate.Length - 1);
        }

        var segments = candidate.Split('/');
        for (var i = 1; i < segments.Length; i++)
        {
            if (segments[i] == "." || segments[i] == "..")
            {
                error = "Route prefix must not contain '.' or '..' path segments.";
                return false;
            }
        }

        normalizedRoutePrefix = candidate;
        error = null;
        return true;
    }
}
