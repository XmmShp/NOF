using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NOF.Contract.SourceGenerator;

internal static class ExposeToHttpEndpointHelpers
{
    public const string PublicApiAttributeFqn = "NOF.Contract.PublicApiAttribute";
    public const string HttpEndpointAttributeFqn = "NOF.Contract.HttpEndpointAttribute";
    public const string GenerateServiceAttributeFqn = "NOF.Contract.GenerateServiceAttribute";

    public static bool HasPublicApiAttribute(INamedTypeSymbol symbol)
    {
        return symbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() == PublicApiAttributeFqn);
    }

    public static bool HasHttpEndpointAttribute(INamedTypeSymbol symbol)
    {
        return symbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() == HttpEndpointAttributeFqn);
    }

    public static bool HasGenerateServiceAttribute(INamedTypeSymbol symbol)
    {
        return symbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() == GenerateServiceAttributeFqn);
    }

    public static bool IsRequestType(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(i =>
            i.ToDisplayString() == "NOF.Contract.IRequest"
            || (i is { IsGenericType: true }
                && i.OriginalDefinition.ToDisplayString() == "NOF.Contract.IRequest<TResponse>"));
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

    /// <summary>
    /// Gets the root (first segment) of a namespace.
    /// e.g. "MyApp.Features.Users" → "MyApp"
    /// </summary>
    public static string? GetRootNamespace(INamespaceSymbol ns)
    {
        while (ns is not null && !string.IsNullOrEmpty(ns.Name))
        {
            if (ns.ContainingNamespace is null || string.IsNullOrEmpty(ns.ContainingNamespace.Name))
            {
                return ns.Name;
            }
            ns = ns.ContainingNamespace;
        }
        return null;
    }

    /// <summary>
    /// Derives the HTTP client class name from the interface name.
    /// e.g. "ISampleService" → "HttpSampleService"
    /// </summary>
    public static string GetHttpClientName(string interfaceName)
    {
        // Remove leading I
        var baseName = interfaceName.StartsWith("I") && interfaceName.Length > 1 && char.IsUpper(interfaceName[1])
            ? interfaceName.Substring(1)
            : interfaceName;
        return $"Http{baseName}";
    }

    public static ITypeSymbol? GetResponseType(INamedTypeSymbol requestType)
    {
        var requestInterface = requestType.AllInterfaces
            .FirstOrDefault(i => i.OriginalDefinition.ToDisplayString() == "NOF.Contract.IRequest<TResponse>"
                                 && i.IsGenericType);

        return requestInterface is { TypeArguments.Length: 1 }
            ? requestInterface.TypeArguments[0]
            : null;
    }

    /// <summary>
    /// Gets the operation name from a PublicApiAttribute on a request type.
    /// Falls back to removing "Request" suffix from the class name.
    /// </summary>
    public static string GetOperationName(INamedTypeSymbol classSymbol)
    {
        var publicApiAttr = classSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == PublicApiAttributeFqn);

        var operationName = publicApiAttr?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "OperationName").Value.Value as string;

        if (!string.IsNullOrEmpty(operationName))
        {
            return operationName!;
        }

        const string requestSuffix = "Request";
        return classSymbol.Name.EndsWith(requestSuffix)
            ? classSymbol.Name.Substring(0, classSymbol.Name.Length - requestSuffix.Length)
            : classSymbol.Name;
    }

    public static EndpointInfo ExtractEndpointInfo(
        INamedTypeSymbol classSymbol,
        AttributeData httpAttr)
    {
        var method = (HttpVerb)httpAttr.ConstructorArguments[0].Value!;
        var route = httpAttr.ConstructorArguments.Length > 1
            ? httpAttr.ConstructorArguments[1].Value as string
            : null;
        route = route?.TrimEnd('/');

        var operationName = GetOperationName(classSymbol);

        route ??= operationName;

        var responseType = GetResponseType(classSymbol);

        var allAttrs = classSymbol.GetAttributes();

        var displayName = allAttrs
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "NOF.Contract.EndpointNameAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;

        var description = allAttrs
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "NOF.Contract.EndpointDescriptionAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;

        var summary = allAttrs
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "NOF.Contract.SummaryAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;

        var tags = allAttrs
            .Where(a => a.AttributeClass?.ToDisplayString() == "System.ComponentModel.CategoryAttribute")
            .Select(a => a.ConstructorArguments.FirstOrDefault().Value as string)
            .Where(v => v != null)
            .ToArray();

        return new EndpointInfo
        {
            RequestType = classSymbol,
            ResponseType = responseType,
            Method = method,
            Route = route,
            OperationName = operationName,
            DisplayName = displayName,
            Description = description,
            Summary = summary,
            Tags = tags!
        };
    }

    /// <summary>
    /// Creates a default EndpointInfo for a request that has [PublicApi] but no [HttpEndpoint].
    /// Defaults to POST with the operation name as the route.
    /// </summary>
    public static EndpointInfo ExtractDefaultEndpointInfo(INamedTypeSymbol classSymbol)
    {
        var operationName = GetOperationName(classSymbol);
        var responseType = GetResponseType(classSymbol);

        var allAttrs = classSymbol.GetAttributes();

        var displayName = allAttrs
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "NOF.Contract.EndpointNameAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;

        var description = allAttrs
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "NOF.Contract.EndpointDescriptionAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;

        var summary = allAttrs
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "NOF.Contract.SummaryAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;

        var tags = allAttrs
            .Where(a => a.AttributeClass?.ToDisplayString() == "System.ComponentModel.CategoryAttribute")
            .Select(a => a.ConstructorArguments.FirstOrDefault().Value as string)
            .Where(v => v != null)
            .ToArray();

        return new EndpointInfo
        {
            RequestType = classSymbol,
            ResponseType = responseType,
            Method = HttpVerb.Post,
            Route = operationName,
            OperationName = operationName,
            DisplayName = displayName,
            Description = description,
            Summary = summary,
            Tags = tags!
        };
    }

    /// <summary>
    /// Extracts a PublicApiInfo from a request type that has [PublicApi] but not necessarily [HttpEndpoint].
    /// </summary>
    public static PublicApiInfo ExtractPublicApiInfo(INamedTypeSymbol classSymbol)
    {
        var operationName = GetOperationName(classSymbol);
        var responseType = GetResponseType(classSymbol);

        var allAttrs = classSymbol.GetAttributes();

        var displayName = allAttrs
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "NOF.Contract.EndpointNameAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;

        var description = allAttrs
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "NOF.Contract.EndpointDescriptionAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;

        var summary = allAttrs
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "NOF.Contract.SummaryAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;

        return new PublicApiInfo
        {
            RequestType = classSymbol,
            ResponseType = responseType,
            OperationName = operationName,
            DisplayName = displayName,
            Description = description,
            Summary = summary
        };
    }

    /// <summary>
    /// Gets the full namespace string from an INamespaceSymbol.
    /// </summary>
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
}

/// <summary>
/// Info for a request type that has [PublicApi] — used for service interface generation.
/// </summary>
internal class PublicApiInfo
{
    public INamedTypeSymbol RequestType { get; set; } = null!;
    public ITypeSymbol? ResponseType { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Summary { get; set; }
}

/// <summary>
/// Info for a request type that has [HttpEndpoint] — extends PublicApiInfo with HTTP-specific data.
/// </summary>
internal class EndpointInfo
{
    public INamedTypeSymbol RequestType { get; set; } = null!;
    public ITypeSymbol? ResponseType { get; set; }
    public HttpVerb Method { get; set; }
    public string Route { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Summary { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
}

internal enum HttpVerb
{
    Get,
    Post,
    Put,
    Delete,
    Patch
}
