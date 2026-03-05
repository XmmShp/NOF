using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.Hosting.AspNetCore.SourceGenerator;

internal static class AssemblyPrefixHelper
{
    private const string AssemblyPrefixAttributeFullName = "NOF.Annotation.AssemblyPrefixAttribute";

    /// <summary>
    /// Returns the primary assembly prefix (first element of <c>[assembly: AssemblyPrefix(...)]</c>),
    /// or falls back to <c>compilation.AssemblyName</c>.
    /// Used for generated namespace and class names.
    /// </summary>
    internal static string GetAssemblyPrefix(Compilation compilation)
    {
        var prefixes = GetPrefixValues(compilation);
        return prefixes.Length > 0 ? prefixes[0] : (compilation.AssemblyName ?? "Unknown");
    }

    /// <summary>
    /// Returns all prefixes specified by <c>[assembly: AssemblyPrefix(...)]</c>.
    /// Used for prefix-matching referenced assemblies.
    /// Falls back to the primary prefix (single element) if no attribute is present.
    /// </summary>
    internal static ImmutableArray<string> GetAllPrefixes(Compilation compilation)
    {
        var prefixes = GetPrefixValues(compilation);
        if (prefixes.Length > 0)
        {
            return prefixes;
        }

        var fallback = compilation.AssemblyName ?? "Unknown";
        return ImmutableArray.Create(fallback);
    }

    private static ImmutableArray<string> GetPrefixValues(Compilation compilation)
    {
        var attr = compilation.Assembly.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == AssemblyPrefixAttributeFullName);

        if (attr is null || attr.ConstructorArguments.Length == 0)
        {
            return ImmutableArray<string>.Empty;
        }

        // params string[] is passed as a single TypedConstant of kind Array
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

        // Single string (shouldn't happen with params, but defensive)
        if (arg.Value is string single && !string.IsNullOrEmpty(single))
        {
            return ImmutableArray.Create(single);
        }

        return ImmutableArray<string>.Empty;
    }
}
