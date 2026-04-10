using Microsoft.CodeAnalysis;

namespace NOF.Application.SourceGenerator;

internal static class SplitInterfaceSymbolHelper
{
    private const string SplitedInterfaceFqn = "NOF.Application.ISplitedInterface<TService>";

    public static bool ImplementsSplitedInterface(INamedTypeSymbol classSymbol)
        => TryGetServiceInterface(classSymbol, out _);

    public static bool TryGetServiceInterface(INamedTypeSymbol classSymbol, out INamedTypeSymbol? serviceInterface)
    {
        serviceInterface = null;

        foreach (var implementedInterface in classSymbol.AllInterfaces)
        {
            if (implementedInterface.IsGenericType
                && implementedInterface.OriginalDefinition.ToDisplayString() == SplitedInterfaceFqn)
            {
                serviceInterface = implementedInterface.TypeArguments[0] as INamedTypeSymbol;
                return serviceInterface is not null;
            }
        }

        return false;
    }
}
