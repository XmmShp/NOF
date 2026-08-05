using Microsoft.CodeAnalysis;
using NOF.SourceGeneration;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF.Infrastructure.SourceGenerator;

internal static class RpcClientHelpers
{
    public const string RpcServiceInterfaceFqn = "NOF.Contract.IRpcService";
    public const string ResultInterfaceFqn = "NOF.Contract.IResult";
    public const string RpcServerFqn = "NOF.Application.RpcServer<TRpcService>";

    public static bool IsRpcServiceInterface(INamedTypeSymbol symbol)
        => symbol.TypeKind == TypeKind.Interface
           && (symbol.ToDisplayString() == RpcServiceInterfaceFqn
               || symbol.AllInterfaces.Any(i => i.ToDisplayString() == RpcServiceInterfaceFqn));

    public static bool TryGetRpcServiceFromRpcServer(
        INamedTypeSymbol rpcServer,
        out INamedTypeSymbol? serviceInterface)
    {
        serviceInterface = null;
        var current = rpcServer;
        while (current is not null)
        {
            if (current.BaseType is INamedTypeSymbol baseType
                && baseType.IsGenericType
                && baseType.OriginalDefinition.ToDisplayString() == RpcServerFqn)
            {
                serviceInterface = baseType.TypeArguments[0] as INamedTypeSymbol;
                return serviceInterface is not null && IsRpcServiceInterface(serviceInterface);
            }

            current = current.BaseType;
        }

        return false;
    }

    public static bool ImplementsResultContract(ITypeSymbol type)
        => type.ToDisplayString() == ResultInterfaceFqn
           || type.AllInterfaces.Any(static i => i.ToDisplayString() == ResultInterfaceFqn);

    public static string GetRpcClientInterfaceTypeName(INamedTypeSymbol serviceInterface)
    {
        var targetNamespace = GetFullNamespace(serviceInterface.ContainingNamespace);
        var typeArguments = serviceInterface.TypeArguments.Length == 0
            ? string.Empty
            : "<" + string.Join(", ", serviceInterface.TypeArguments.Select(
                static argument => argument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))) + ">";
        var typeName = RpcContractConventions.GetClientInterfaceName(serviceInterface.Name) + typeArguments;
        return string.IsNullOrWhiteSpace(targetNamespace)
            ? "global::" + typeName
            : "global::" + targetNamespace + "." + typeName;
    }

    public static string GetLocalClientName(INamedTypeSymbol rpcServer)
        => RpcContractConventions.GetLocalClientName(rpcServer.Name);

    public static string GetFullNamespace(INamespaceSymbol ns)
    {
        var parts = new List<string>();
        while (ns is not null && !string.IsNullOrEmpty(ns.Name))
        {
            parts.Insert(0, ns.Name);
            ns = ns.ContainingNamespace;
        }

        return string.Join(".", parts);
    }

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
