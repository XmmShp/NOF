using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF.SourceGenerator.Shared;

internal static class SymbolDiscoveryHelper
{
    internal static ImmutableArray<INamedTypeSymbol> CollectTypes(
        Compilation compilation,
        ImmutableArray<string> allPrefixes,
        System.Func<INamedTypeSymbol, bool> predicate)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        CollectFromNamespace(compilation.Assembly.GlobalNamespace, builder, seen, predicate);

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (!AssemblyPrefixHelper.MatchesPrefix(assembly.Name, allPrefixes))
            {
                continue;
            }

            CollectFromNamespace(assembly.GlobalNamespace, builder, seen, predicate);
        }

        return builder
            .OrderBy(static t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), System.StringComparer.Ordinal)
            .ToImmutableArray();
    }

    internal static string SanitizeIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        if (builder.Length == 0 || !char.IsLetter(builder[0]) && builder[0] != '_')
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    private static void CollectFromNamespace(
        INamespaceSymbol ns,
        ImmutableArray<INamedTypeSymbol>.Builder builder,
        HashSet<INamedTypeSymbol> seen,
        System.Func<INamedTypeSymbol, bool> predicate)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            CollectFromType(type, builder, seen, predicate);
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            CollectFromNamespace(child, builder, seen, predicate);
        }
    }

    private static void CollectFromType(
        INamedTypeSymbol type,
        ImmutableArray<INamedTypeSymbol>.Builder builder,
        HashSet<INamedTypeSymbol> seen,
        System.Func<INamedTypeSymbol, bool> predicate)
    {
        if (predicate(type) && seen.Add(type))
        {
            builder.Add(type);
        }

        foreach (var nested in type.GetTypeMembers())
        {
            CollectFromType(nested, builder, seen, predicate);
        }
    }
}
