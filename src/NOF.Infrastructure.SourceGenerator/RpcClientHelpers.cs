using Microsoft.CodeAnalysis;
using System;
using System.Linq;

namespace NOF.Infrastructure.SourceGenerator;

internal static class RpcClientHelpers
{
    public const string RpcClientInterfaceFqn = "NOF.Contract.IRpcClient";
    public const string RpcServiceInterfaceFqn = "NOF.Contract.IRpcService";
    public const string LocalRpcClientAttributeFqn = "NOF.Infrastructure.LocalRpcClientAttribute<TRpcClient>";

    public static bool IsRpcClientInterface(INamedTypeSymbol symbol)
        => symbol.TypeKind == TypeKind.Interface
           && (symbol.ToDisplayString() == RpcClientInterfaceFqn
               || symbol.AllInterfaces.Any(i => i.ToDisplayString() == RpcClientInterfaceFqn));

    public static bool IsRpcServiceInterface(INamedTypeSymbol symbol)
        => symbol.TypeKind == TypeKind.Interface
           && (symbol.ToDisplayString() == RpcServiceInterfaceFqn
               || symbol.AllInterfaces.Any(i => i.ToDisplayString() == RpcServiceInterfaceFqn));

    public static bool TryGetRpcServiceFromClientInterface(INamedTypeSymbol clientInterface, out INamedTypeSymbol? serviceInterface)
    {
        serviceInterface = null;
        if (!IsRpcClientInterface(clientInterface) || !clientInterface.Name.EndsWith("Client", StringComparison.Ordinal))
        {
            return false;
        }

        var serviceInterfaceName = clientInterface.Name.Substring(0, clientInterface.Name.Length - "Client".Length);
        if (string.IsNullOrWhiteSpace(serviceInterfaceName))
        {
            return false;
        }

        var ns = GetFullNamespace(clientInterface.ContainingNamespace);
        var metadataName = string.IsNullOrWhiteSpace(ns)
            ? serviceInterfaceName
            : $"{ns}.{serviceInterfaceName}";

        serviceInterface = clientInterface.ContainingAssembly.GetTypeByMetadataName(metadataName);
        return serviceInterface is not null && IsRpcServiceInterface(serviceInterface);
    }

    public static bool TryGetRpcClientFromLocalRpcClientAttribute(INamedTypeSymbol classSymbol, out INamedTypeSymbol? clientInterface)
    {
        clientInterface = null;
        var attribute = classSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.IsGenericType == true
                                 && a.AttributeClass.OriginalDefinition.ToDisplayString() == LocalRpcClientAttributeFqn);
        if (attribute?.AttributeClass?.TypeArguments.Length != 1)
        {
            return false;
        }

        clientInterface = attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
        return clientInterface is not null;
    }

    public static string GetFullNamespace(INamespaceSymbol ns)
    {
        var parts = new System.Collections.Generic.List<string>();
        while (ns is not null && !string.IsNullOrEmpty(ns.Name))
        {
            parts.Insert(0, ns.Name);
            ns = ns.ContainingNamespace;
        }

        return string.Join(".", parts);
    }
}
