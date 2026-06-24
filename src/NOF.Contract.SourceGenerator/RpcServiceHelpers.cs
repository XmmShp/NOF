using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF.Contract.SourceGenerator;

internal static class RpcServiceHelpers
{
    public const string HttpEndpointAttributeFqn = "NOF.Contract.HttpEndpointAttribute";
    public const string FromHeaderAttributeFqn = "NOF.Contract.FromHeaderAttribute";
    public const string TransportStringParsableFqn = "NOF.Contract.ITransportStringParsable<TSelf>";
    public const string SummaryAttributeFqn = "NOF.Contract.SummaryAttribute";
    public const string RpcServiceInterfaceFqn = "NOF.Contract.IRpcService";
    public const string ResultInterfaceFqn = "NOF.Contract.IResult";
    public const string StreamingResultFqn = "NOF.Contract.StreamingResult<T>";
    public const string ResultFqn = "NOF.Contract.Result";
    public const string GenericResultFqn = "NOF.Contract.Result<T>";
    public const string EmptyFqn = "NOF.Contract.Empty";

    public static bool HasHttpEndpointAttribute(IMethodSymbol symbol)
    {
        return symbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() == HttpEndpointAttributeFqn);
    }

    public static bool IsRpcServiceInterface(INamedTypeSymbol symbol)
        => symbol.TypeKind == TypeKind.Interface
           && (symbol.ToDisplayString() == RpcServiceInterfaceFqn
               || symbol.AllInterfaces.Any(i => i.ToDisplayString() == RpcServiceInterfaceFqn));

    public static List<IPropertySymbol> GetAllPublicProperties(INamedTypeSymbol typeSymbol)
    {
        var properties = new List<IPropertySymbol>();
        var seen = new HashSet<string>();
        var current = typeSymbol;
        while (current != null)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false, IsIndexer: false, GetMethod: not null } prop
                    && seen.Add(prop.Name))
                {
                    properties.Add(prop);
                }
            }

            current = current.BaseType;
        }

        return properties;
    }

    public static bool ContainsRouteParameters(string route)
    {
        return route.IndexOf('{') >= 0 || route.IndexOf('}') >= 0;
    }

    public static string GetHttpClientName(string interfaceName)
    {
        var baseName = GetServiceBaseName(interfaceName);
        return $"Http{baseName}";
    }

    public static string GetClientInterfaceName(string interfaceName)
        => interfaceName + "Client";

    public static string GetServiceBaseName(string interfaceName)
    {
        return interfaceName.StartsWith("I") && interfaceName.Length > 1 && char.IsUpper(interfaceName[1])
            ? interfaceName.Substring(1)
            : interfaceName;
    }

    public static string GetOperationName(string methodName)
    {
        return methodName.EndsWith("Async", StringComparison.Ordinal)
            ? methodName.Substring(0, methodName.Length - 5)
            : methodName;
    }

    public static bool IsCancellationToken(ITypeSymbol typeSymbol)
        => typeSymbol.ToDisplayString() == "System.Threading.CancellationToken";

    public static bool TryGetRequestParameter(IMethodSymbol method, out IParameterSymbol? parameter)
    {
        if (method.Parameters.Length == 1 && !IsCancellationToken(method.Parameters[0].Type))
        {
            parameter = method.Parameters[0];
            return true;
        }

        parameter = null;
        return false;
    }

    public static bool TryGetServiceReturnInfo(IMethodSymbol method, out ServiceReturnInfo returnInfo)
    {
        if (method.ReturnsVoid)
        {
            returnInfo = default;
            return false;
        }

        var returnType = method.ReturnType;
        if (returnType.ToDisplayString() is "System.Threading.Tasks.Task"
            or "System.Threading.Tasks.ValueTask")
        {
            returnInfo = default;
            return false;
        }

        if (returnType is INamedTypeSymbol { IsGenericType: true } namedType
            && (namedType.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.Task<T>"
                || namedType.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.ValueTask<T>"))
        {
            returnInfo = default;
            return false;
        }

        if (!ImplementsResultContract(returnType))
        {
            returnInfo = default;
            return false;
        }

        returnInfo = new ServiceReturnInfo(returnType);
        return true;
    }

    public static bool ImplementsResultContract(ITypeSymbol type)
    {
        return type.ToDisplayString() == ResultInterfaceFqn
               || type.AllInterfaces.Any(i => i.ToDisplayString() == ResultInterfaceFqn);
    }

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

    public static EndpointInfo ExtractEndpointInfo(ServiceMethodInfo method)
    {
        var attributes = method.Method.GetAttributes();
        var methodAttr = attributes
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == HttpEndpointAttributeFqn);

        var httpMethod = methodAttr is null
            ? HttpVerb.Post
            : (HttpVerb)methodAttr.ConstructorArguments[0].Value!;

        var route = methodAttr is null
            ? method.OperationName
            : methodAttr.ConstructorArguments.Length > 1
                ? methodAttr.ConstructorArguments[1].Value as string
                : null;

        route = route?.TrimEnd('/');
        route ??= method.OperationName;

        var displayName = TryGetCtorString(attributes, "NOF.Contract.EndpointNameAttribute");

        var description = TryGetCtorString(attributes, "System.ComponentModel.DescriptionAttribute");

        var summary = TryGetCtorString(attributes, SummaryAttributeFqn);

        var tags = attributes
            .Where(a => a.AttributeClass?.ToDisplayString() == "System.ComponentModel.CategoryAttribute")
            .Select(a => a.ConstructorArguments.FirstOrDefault().Value as string)
            .Where(v => v != null)
            .Cast<string>()
            .ToArray();

        return new EndpointInfo
        {
            RequestType = method.RequestType,
            ReturnInfo = method.ReturnInfo,
            Method = httpMethod,
            Route = route,
            OperationName = method.OperationName,
            DisplayName = displayName,
            Description = description,
            Summary = summary,
            Tags = tags
        };
    }

    private static string? TryGetCtorString(ImmutableArray<AttributeData> attributes, string attributeFqn)
        => attributes
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == attributeFqn)
            ?.ConstructorArguments.FirstOrDefault().Value as string;

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

internal sealed class ServiceMethodInfo
{
    public IMethodSymbol Method { get; set; } = null!;
    public INamedTypeSymbol? RequestType { get; set; }
    public ServiceReturnInfo ReturnInfo { get; set; } = new(null!);
    public string OperationName { get; set; } = string.Empty;
}

internal class EndpointInfo
{
    public INamedTypeSymbol? RequestType { get; set; }
    public ServiceReturnInfo ReturnInfo { get; set; } = new(null!);
    public HttpVerb Method { get; set; }
    public string Route { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Summary { get; set; }
    public string[] Tags { get; set; } = [];
}

internal readonly struct ServiceReturnInfo
{
    public ServiceReturnInfo(ITypeSymbol valueType)
    {
        ValueType = valueType;
    }

    public ITypeSymbol ValueType { get; }

    public bool IsEmpty
        => ValueType.ToDisplayString() == RpcServiceHelpers.EmptyFqn;

    public bool IsNonGenericResult
        => ValueType.ToDisplayString() == RpcServiceHelpers.ResultFqn;

    public bool IsGenericResult
        => ValueType is INamedTypeSymbol { IsGenericType: true } namedType
           && namedType.OriginalDefinition.ToDisplayString() == RpcServiceHelpers.GenericResultFqn;

    public bool IsStream
        => ValueType is INamedTypeSymbol { IsGenericType: true } namedType
           && namedType.OriginalDefinition.ToDisplayString() == RpcServiceHelpers.StreamingResultFqn;

    public ITypeSymbol? StreamItemType
        => ValueType is INamedTypeSymbol { IsGenericType: true } namedType && namedType.TypeArguments.Length == 1
            ? namedType.TypeArguments[0]
            : null;

    public string ClientResponseTypeDisplay
        => ValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    public string ClientTaskReturnTypeDisplay
        => $"global::System.Threading.Tasks.Task<{ClientResponseTypeDisplay}>";
}

internal enum HttpVerb
{
    Get,
    Post,
    Put,
    Delete,
    Patch
}
