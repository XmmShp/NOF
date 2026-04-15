using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace NOF.Hosting.AspNetCore.SourceGenerator;

internal static class RpcServiceHelpers
{
    public const string HttpEndpointAttributeFqn = "NOF.Contract.HttpEndpointAttribute";
    public const string SummaryAttributeFqn = "NOF.Contract.SummaryAttribute";
    public const string RpcServiceInterfaceFqn = "NOF.Contract.IRpcService";

    public static bool IsRpcServiceInterface(INamedTypeSymbol symbol)
        => symbol.TypeKind == TypeKind.Interface
           && (symbol.ToDisplayString() == RpcServiceInterfaceFqn
               || symbol.AllInterfaces.Any(i => i.ToDisplayString() == RpcServiceInterfaceFqn));

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
            ServiceType = method.Method.ContainingType,
            ServiceMethodName = method.Method.Name,
            ServiceHasCancellationToken = method.Method.Parameters.Any(p => IsCancellationToken(p.Type)),
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
    public INamedTypeSymbol ServiceType { get; set; } = null!;
    public string ServiceMethodName { get; set; } = string.Empty;
    public bool ServiceHasCancellationToken { get; set; }
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

internal enum HttpVerb
{
    Get,
    Post,
    Put,
    Delete,
    Patch
}

internal readonly struct ServiceReturnInfo
{
    public ServiceReturnInfo(ITypeSymbol valueType)
    {
        ValueType = valueType;
    }

    public ITypeSymbol ValueType { get; }
}
