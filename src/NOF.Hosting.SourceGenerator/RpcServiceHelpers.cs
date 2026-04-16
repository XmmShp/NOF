using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace NOF.Hosting.SourceGenerator;

internal static class RpcServiceHelpers
{
    public const string HttpEndpointAttributeFqn = "NOF.Contract.HttpEndpointAttribute";
    public const string SummaryAttributeFqn = "NOF.Contract.SummaryAttribute";
    public const string RpcServiceInterfaceFqn = "NOF.Contract.IRpcService";
    public const string RpcClientInterfaceFqn = "NOF.Contract.IRpcClient";
    public const string HttpRpcClientAttributeFqn = "NOF.Hosting.HttpRpcClientAttribute<TRpcClient>";
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

    public static List<string> ExtractRouteParameters(string route)
    {
        var result = new List<string>();
        var matches = Regex.Matches(route, @"\{(\w+)\}");
        foreach (Match match in matches)
        {
            result.Add(match.Groups[1].Value);
        }

        return result;
    }

    public static string GetHttpClientName(string interfaceName)
    {
        var baseName = GetServiceBaseName(interfaceName);
        return $"Http{baseName}";
    }

    public static string GetClientInterfaceName(string interfaceName)
        => interfaceName + "Client";

    public static bool IsRpcClientInterface(INamedTypeSymbol symbol)
        => symbol.TypeKind == TypeKind.Interface
           && (symbol.ToDisplayString() == RpcClientInterfaceFqn
               || symbol.AllInterfaces.Any(i => i.ToDisplayString() == RpcClientInterfaceFqn));

    public static bool TryGetRpcClientFromHttpRpcClientAttribute(INamedTypeSymbol classSymbol, out INamedTypeSymbol? clientInterface)
    {
        clientInterface = null;
        var attribute = classSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.IsGenericType == true
                                 && a.AttributeClass.OriginalDefinition.ToDisplayString() == HttpRpcClientAttributeFqn);
        if (attribute?.AttributeClass?.TypeArguments.Length != 1)
        {
            return false;
        }

        clientInterface = attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
        return clientInterface is not null;
    }

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

        returnInfo = new ServiceReturnInfo(returnType);
        return true;
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

    public string NormalizedResultTypeDisplay
    {
        get
        {
            if (IsEmpty || IsNonGenericResult)
            {
                return "global::NOF.Contract.Result";
            }

            if (IsGenericResult)
            {
                return ValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            return $"global::NOF.Contract.Result<{ValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>";
        }
    }

    public string ClientTaskReturnTypeDisplay
        => $"global::System.Threading.Tasks.Task<{NormalizedResultTypeDisplay}>";
}

internal enum HttpVerb
{
    Get,
    Post,
    Put,
    Delete,
    Patch
}
