using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

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

        serviceInterface = clientInterface.ContainingType is not null
            ? clientInterface.ContainingType.GetTypeMembers(serviceInterfaceName, clientInterface.Arity).FirstOrDefault()
            : clientInterface.ContainingNamespace.GetTypeMembers(serviceInterfaceName, clientInterface.Arity).FirstOrDefault();
        if (serviceInterface is { IsGenericType: true } genericService && clientInterface.TypeArguments.Length == genericService.TypeParameters.Length)
        {
            serviceInterface = genericService.Construct(clientInterface.TypeArguments.ToArray());
        }

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

    public static string GetTypeDeclarationName(INamedTypeSymbol symbol)
        => symbol.Name + GetTypeParameterList(symbol.TypeParameters);

    public static string GetTypeParameterList(ImmutableArray<ITypeParameterSymbol> typeParameters)
    {
        if (typeParameters.Length == 0)
        {
            return string.Empty;
        }

        return "<" + string.Join(", ", typeParameters.Select(static parameter => parameter.Name)) + ">";
    }

    public static void AppendTypeParameterConstraints(StringBuilder sb, INamedTypeSymbol symbol, int indentLevel)
    {
        foreach (var typeParameter in symbol.TypeParameters)
        {
            var constraints = BuildTypeParameterConstraints(typeParameter);
            if (constraints.Count == 0)
            {
                continue;
            }

            sb.Append(' ', indentLevel * 4);
            sb.Append("where ");
            sb.Append(typeParameter.Name);
            sb.Append(" : ");
            sb.AppendLine(string.Join(", ", constraints));
        }
    }

    private static List<string> BuildTypeParameterConstraints(ITypeParameterSymbol typeParameter)
    {
        var constraints = new List<string>();
        if (typeParameter.HasReferenceTypeConstraint)
        {
            constraints.Add(typeParameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
        }
        else if (typeParameter.HasValueTypeConstraint)
        {
            constraints.Add("struct");
        }
        else if (typeParameter.HasUnmanagedTypeConstraint)
        {
            constraints.Add("unmanaged");
        }
        else if (typeParameter.HasNotNullConstraint)
        {
            constraints.Add("notnull");
        }

        constraints.AddRange(typeParameter.ConstraintTypes.Select(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));

        if (typeParameter.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        return constraints;
    }
}
