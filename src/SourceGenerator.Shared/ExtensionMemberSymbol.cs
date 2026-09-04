using Microsoft.CodeAnalysis;

namespace NOF.SourceGenerator.Shared;

internal static class ExtensionMemberSymbol
{
    public static bool IsDeclaredBy(
        IMethodSymbol method,
        INamedTypeSymbol extensionContainer)
    {
        if (IsDeclaredByCore(method, extensionContainer))
        {
            return true;
        }

        var reducedMethod = method.ReducedFrom;
        if (reducedMethod is not null
            && IsDeclaredByCore(reducedMethod, extensionContainer))
        {
            return true;
        }

        if (method.AssociatedExtensionImplementation is { } implementationMethod
            && IsDeclaredByCore(implementationMethod, extensionContainer))
        {
            return true;
        }

        return reducedMethod?.AssociatedExtensionImplementation is { } reducedImplementationMethod
            && IsDeclaredByCore(reducedImplementationMethod, extensionContainer);
    }

    private static bool IsDeclaredByCore(
        IMethodSymbol method,
        INamedTypeSymbol extensionContainer)
    {
        var containingType = method.ContainingType;
        return SymbolEqualityComparer.Default.Equals(containingType, extensionContainer)
            || containingType.IsExtension
                && SymbolEqualityComparer.Default.Equals(
                    containingType.ContainingType,
                    extensionContainer);
    }
}
