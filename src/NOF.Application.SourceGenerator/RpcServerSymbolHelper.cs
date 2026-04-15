using Microsoft.CodeAnalysis;

namespace NOF.Application.SourceGenerator;

internal static class RpcServerSymbolHelper
{
    private const string RpcServerFqn = "NOF.Application.RpcServer<TRpcService>";
    private const string RpcHandlerFqn = "NOF.Application.RpcHandler<TRequest, TResponse>";

    public static bool ImplementsRpcServer(INamedTypeSymbol classSymbol)
        => TryGetServiceInterface(classSymbol, out _);

    public static bool TryGetServiceInterface(INamedTypeSymbol classSymbol, out INamedTypeSymbol? serviceInterface)
    {
        serviceInterface = null;

        var current = classSymbol;
        while (current is not null)
        {
            if (current.BaseType is INamedTypeSymbol baseType
                && baseType.IsGenericType
                && baseType.OriginalDefinition.ToDisplayString() == RpcServerFqn)
            {
                serviceInterface = baseType.TypeArguments[0] as INamedTypeSymbol;
                return serviceInterface is not null;
            }

            current = current.BaseType;
        }

        return false;
    }

    public static bool TryGetRpcHandlerBase(INamedTypeSymbol typeSymbol, out INamedTypeSymbol? handlerBaseType, out INamedTypeSymbol? serverType)
    {
        handlerBaseType = null;
        serverType = null;

        var current = typeSymbol.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType
                && current.OriginalDefinition.ToDisplayString() == RpcHandlerFqn
                && current.ContainingType is INamedTypeSymbol containingType
                && TryGetServiceInterface(containingType, out _))
            {
                handlerBaseType = current;
                serverType = containingType;
                return true;
            }

            if (current.ContainingType is INamedTypeSymbol nestedContainingType
                && TryGetServiceInterface(nestedContainingType, out _))
            {
                handlerBaseType = current;
                serverType = nestedContainingType;
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
