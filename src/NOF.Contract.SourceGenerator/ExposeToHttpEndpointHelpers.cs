using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NOF.Contract.SourceGenerator;

internal static class ExposeToHttpEndpointHelpers
{
    public static bool HasExposeToHttpEndpointAttribute(INamedTypeSymbol symbol)
    {
        return symbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() == "NOF.Contract.ExposeToHttpEndpointAttribute");
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
    /// Derives the service name from the assembly name.
    /// Removes dots, strips trailing "Contract" if present, appends "Service" if not already ending with it,
    /// then derives interface and client names.
    /// e.g. "NOF.Sample.Contract" → ServiceName="NOFSampleService", InterfaceName="INOFSampleService", ClientName="NOFSampleServiceClient"
    /// </summary>
    public static (string ServiceName, string InterfaceName, string ClientName) GetServiceNames(string assemblyName)
    {
        var sanitized = assemblyName.Replace(".", "");
        const string contractSuffix = "Contract";
        if (sanitized.EndsWith(contractSuffix))
        {
            sanitized = sanitized.Substring(0, sanitized.Length - contractSuffix.Length);
        }
        const string serviceSuffix = "Service";
        if (!sanitized.EndsWith(serviceSuffix))
        {
            sanitized = sanitized + serviceSuffix;
        }
        var interfaceName = $"I{sanitized}";
        var clientName = $"{sanitized}Client";
        return (sanitized, interfaceName, clientName);
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

    public static EndpointInfo ExtractEndpointInfo(
        INamedTypeSymbol classSymbol,
        AttributeData attr,
        Compilation compilation)
    {
        var method = (HttpVerb)attr.ConstructorArguments[0].Value!;
        var route = attr.ConstructorArguments.Length > 1
            ? attr.ConstructorArguments[1].Value as string
            : null;
        route = route?.TrimEnd('/');

        const string requestSuffix = "Request";
        var operationName = attr.NamedArguments
            .FirstOrDefault(arg => arg.Key == "OperationName").Value.Value as string
            ?? (classSymbol.Name.EndsWith(requestSuffix)
                ? classSymbol.Name.Substring(0, classSymbol.Name.Length - requestSuffix.Length)
                : classSymbol.Name);

        route ??= operationName;

        var responseType = GetResponseType(classSymbol);
        var permission = attr.NamedArguments
            .FirstOrDefault(arg => arg.Key == "Permission").Value.Value as string;
        var allowAnonymous = attr.NamedArguments
            .FirstOrDefault(arg => arg.Key == "AllowAnonymous").Value.Value is true;

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
            Permission = permission,
            AllowAnonymous = allowAnonymous,
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
    public string? Permission { get; set; }
    public bool AllowAnonymous { get; set; }
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
