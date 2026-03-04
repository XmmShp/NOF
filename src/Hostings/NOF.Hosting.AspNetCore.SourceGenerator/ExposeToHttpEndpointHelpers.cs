using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NOF.Hosting.AspNetCore.SourceGenerator;

internal static class ExposeToHttpEndpointHelpers
{
    public const string PublicApiAttributeFqn = "NOF.Contract.PublicApiAttribute";
    public const string HttpEndpointAttributeFqn = "NOF.Contract.HttpEndpointAttribute";
    public const string GenerateServiceAttributeFqn = "NOF.Contract.GenerateServiceAttribute";

    public static bool HasHttpEndpointAttribute(INamedTypeSymbol symbol)
    {
        return symbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() == HttpEndpointAttributeFqn);
    }

    public static bool HasPublicApiAttribute(INamedTypeSymbol symbol)
    {
        return symbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() == PublicApiAttributeFqn);
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
}

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
