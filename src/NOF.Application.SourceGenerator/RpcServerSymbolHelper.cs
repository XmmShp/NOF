using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace NOF.Application.SourceGenerator;

internal static class RpcServerSymbolHelper
{
    private const string RpcServerFqn = "NOF.Application.RpcServer<TRpcService>";

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

    /// <summary>
    /// Resolves a user-authored reference such as <c>MyServer.Ping</c> without requiring
    /// the nested <c>Ping</c> type emitted by <see cref="RpcServerGenerator"/> to exist in
    /// this generator's input compilation.
    /// </summary>
    public static bool TryGetGeneratedRpcHandlerBaseName(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        out string? handlerBaseTypeName)
    {
        handlerBaseTypeName = null;

        foreach (var baseType in classDeclaration.BaseList?.Types ?? [])
        {
            if (baseType.Type is not QualifiedNameSyntax qualifiedName
                || TryGetNamedTypeSymbol(semanticModel, qualifiedName.Left) is not { } serverType
                || !TryGetServiceInterface(serverType, out var serviceInterface)
                || serviceInterface is null)
            {
                continue;
            }

            var operationName = qualifiedName.Right.Identifier.ValueText;
            var generatedHandlerExists = serviceInterface.GetMembers(operationName)
                .OfType<IMethodSymbol>()
                .Any(static method => method.MethodKind == MethodKind.Ordinary
                    && !method.IsImplicitlyDeclared
                    && method.Parameters.Length == 1
                    && method.Parameters[0].Type is INamedTypeSymbol);
            if (!generatedHandlerExists)
            {
                continue;
            }

            handlerBaseTypeName = serverType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                + "."
                + operationName;
            return true;
        }

        return false;
    }

    private static INamedTypeSymbol? TryGetNamedTypeSymbol(SemanticModel semanticModel, NameSyntax syntax)
    {
        if (syntax is IdentifierNameSyntax identifierName
            && semanticModel.GetAliasInfo(identifierName)?.Target is INamedTypeSymbol aliasTarget)
        {
            return aliasTarget;
        }

        var symbolInfo = semanticModel.GetSymbolInfo(syntax);
        return symbolInfo.Symbol as INamedTypeSymbol
            ?? symbolInfo.CandidateSymbols.OfType<INamedTypeSymbol>().FirstOrDefault();
    }
}
