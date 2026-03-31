using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.SourceGenerator.Shared;

internal static class AssemblyPrefixHelper
{
    private const string AssemblyPrefixAttributeFullName = "NOF.Annotation.AssemblyPrefixAttribute";

    internal static string GetAssemblyPrefix(Compilation compilation)
    {
        var prefixes = GetPrefixValues(compilation);
        return prefixes.Length > 0 ? prefixes[0] : (compilation.AssemblyName ?? "Unknown");
    }

    internal static ImmutableArray<string> GetAllPrefixes(Compilation compilation)
    {
        var prefixes = GetPrefixValues(compilation);
        if (prefixes.Length > 0)
        {
            return prefixes;
        }

        var fallback = compilation.AssemblyName ?? "Unknown";
        return [fallback];
    }

    internal static bool MatchesPrefix(string assemblyName, ImmutableArray<string> allPrefixes)
    {
        foreach (var prefix in allPrefixes)
        {
            if (!string.IsNullOrEmpty(prefix) && assemblyName.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ImmutableArray<string> GetPrefixValues(Compilation compilation)
    {
        var attr = compilation.Assembly.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == AssemblyPrefixAttributeFullName);

        if (attr is null || attr.ConstructorArguments.Length == 0)
        {
            return ImmutableArray<string>.Empty;
        }

        var arg = attr.ConstructorArguments[0];
        if (arg.Kind == TypedConstantKind.Array)
        {
            var builder = ImmutableArray.CreateBuilder<string>(arg.Values.Length);
            foreach (var v in arg.Values)
            {
                if (v.Value is string s && !string.IsNullOrEmpty(s))
                {
                    builder.Add(s);
                }
            }

            return builder.ToImmutable();
        }

        if (arg.Value is string single && !string.IsNullOrEmpty(single))
        {
            return [single];
        }

        return ImmutableArray<string>.Empty;
    }
}
