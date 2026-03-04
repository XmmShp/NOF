using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF.Hosting.AspNetCore.SourceGenerator;

[Generator]
public class ExposeToHttpEndpointMapperGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Use CompilationProvider to scan both source and referenced assemblies for [GenerateService]
        context.RegisterSourceOutput(context.CompilationProvider, static (ctx, compilation) => Generate(ctx, compilation));
    }

    private static void Generate(SourceProductionContext context, Compilation compilation)
    {
        // Collect [GenerateService] interfaces from source and referenced assemblies
        var generateServiceInterfaces = new List<INamedTypeSymbol>();

        CollectGenerateServiceInterfaces(compilation.Assembly.GlobalNamespace, generateServiceInterfaces);
        foreach (var refAsm in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            CollectGenerateServiceInterfaces(refAsm.GlobalNamespace, generateServiceInterfaces);
        }

        if (generateServiceInterfaces.Count == 0)
        {
            return;
        }

        // Collect all scan namespaces and extra types from all [GenerateService] interfaces
        var scanNamespaces = new HashSet<string>(StringComparer.Ordinal);
        var extraTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var iface in generateServiceInterfaces)
        {
            var attr = iface.GetAttributes()
                .First(a => a.AttributeClass?.ToDisplayString() == ExposeToHttpEndpointHelpers.GenerateServiceAttributeFqn);

            var namespacesArg = attr.NamedArguments
                .FirstOrDefault(a => a.Key == "Namespaces").Value;

            if (namespacesArg.Kind == TypedConstantKind.Array && !namespacesArg.Values.IsDefaultOrEmpty)
            {
                foreach (var ns in namespacesArg.Values)
                {
                    if (ns.Value is string s)
                    {
                        scanNamespaces.Add(s);
                    }
                }
            }
            else
            {
                var ifaceNs = ExposeToHttpEndpointHelpers.GetFullNamespace(iface.ContainingNamespace);
                if (!string.IsNullOrEmpty(ifaceNs))
                {
                    scanNamespaces.Add(ifaceNs);
                }
            }

            var extraTypesArg = attr.NamedArguments
                .FirstOrDefault(a => a.Key == "ExtraTypes").Value;
            if (extraTypesArg.Kind == TypedConstantKind.Array && !extraTypesArg.Values.IsDefaultOrEmpty)
            {
                foreach (var et in extraTypesArg.Values)
                {
                    if (et.Value is INamedTypeSymbol extraType)
                    {
                        extraTypes.Add(extraType);
                    }
                }
            }
        }

        // Scan for [PublicApi] request types in the specified namespaces
        var seenTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var endpointInfos = new List<EndpointInfo>();

        CollectPublicApiTypes(compilation.Assembly.GlobalNamespace, scanNamespaces, endpointInfos, seenTypes);
        foreach (var refAsm in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            CollectPublicApiTypes(refAsm.GlobalNamespace, scanNamespaces, endpointInfos, seenTypes);
        }

        // Add extra types
        foreach (var extra in extraTypes)
        {
            if (seenTypes.Add(extra)
                && ExposeToHttpEndpointHelpers.HasPublicApiAttribute(extra)
                && ExposeToHttpEndpointHelpers.IsRequestType(extra))
            {
                endpointInfos.Add(GetEndpointInfo(extra));
            }
        }

        if (endpointInfos.Count == 0)
        {
            return;
        }

        GenerateMapAllHttpEndpointsExtension(context, compilation.AssemblyName ?? "Unknown", endpointInfos.ToImmutableArray());
    }

    private static void CollectGenerateServiceInterfaces(INamespaceSymbol ns, List<INamedTypeSymbol> results)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamedTypeSymbol { TypeKind: TypeKind.Interface } type
                    when ExposeToHttpEndpointHelpers.HasGenerateServiceAttribute(type):
                    results.Add(type);
                    break;
                case INamespaceSymbol nestedNs:
                    CollectGenerateServiceInterfaces(nestedNs, results);
                    break;
            }
        }
    }

    private static void CollectPublicApiTypes(INamespaceSymbol ns, HashSet<string> scanNamespaces,
        List<EndpointInfo> results, HashSet<INamedTypeSymbol> seen)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamedTypeSymbol { DeclaredAccessibility: Accessibility.Public, IsAbstract: false } type
                    when ExposeToHttpEndpointHelpers.HasPublicApiAttribute(type)
                         && ExposeToHttpEndpointHelpers.IsRequestType(type):
                {
                    var typeNs = ExposeToHttpEndpointHelpers.GetFullNamespace(type.ContainingNamespace);
                    if (scanNamespaces.Any(scan => typeNs.StartsWith(scan, StringComparison.Ordinal))
                        && seen.Add(type))
                    {
                        results.Add(GetEndpointInfo(type));
                    }
                    break;
                }
                case INamespaceSymbol nestedNs:
                    CollectPublicApiTypes(nestedNs, scanNamespaces, results, seen);
                    break;
            }
        }
    }

    /// <summary>
    /// Gets EndpointInfo for a [PublicApi] request type.
    /// Uses [HttpEndpoint] if present, otherwise defaults to POST.
    /// </summary>
    private static EndpointInfo GetEndpointInfo(INamedTypeSymbol type)
    {
        if (ExposeToHttpEndpointHelpers.HasHttpEndpointAttribute(type))
        {
            var httpAttr = type.GetAttributes()
                .First(a => a.AttributeClass?.ToDisplayString() == ExposeToHttpEndpointHelpers.HttpEndpointAttributeFqn);
            return ExposeToHttpEndpointHelpers.ExtractEndpointInfo(type, httpAttr);
        }

        return ExposeToHttpEndpointHelpers.ExtractDefaultEndpointInfo(type);
    }

    private static void GenerateMapAllHttpEndpointsExtension(SourceProductionContext context, string assemblyName, ImmutableArray<EndpointInfo> endpoints)
    {
        if (endpoints.IsEmpty)
        {
            return;
        }


        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS1591");
        sb.AppendLine();
        sb.AppendLine($"namespace {assemblyName}");
        sb.AppendLine("{");

        // Generate Body DTO classes for non-GET endpoints with route params that have body properties
        foreach (var ep in endpoints)
        {
            EmitBodyDtoIfNeeded(sb, ep);
        }

        var sanitizedAssemblyName = assemblyName.Replace(".", "");
        sb.AppendLine($"    public static partial class {sanitizedAssemblyName}Extensions");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all HTTP endpoints marked with [HttpEndpoint].");
        sb.AppendLine("        /// Generated by source generator.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static global::Microsoft.AspNetCore.Builder.WebApplication MapAllHttpEndpoints(this global::Microsoft.AspNetCore.Builder.WebApplication app)");
        sb.AppendLine("        {");

        foreach (var ep in endpoints)
        {
            EmitEndpointMapping(sb, ep);
        }

        sb.AppendLine("            return app;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("WebApplicationExtensions.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void EmitEndpointMapping(StringBuilder sb, EndpointInfo ep)
    {
        var mapMethod = ep.Method switch
        {
            HttpVerb.Get => "MapGet",
            HttpVerb.Post => "MapPost",
            HttpVerb.Put => "MapPut",
            HttpVerb.Delete => "MapDelete",
            HttpVerb.Patch => "MapPatch",
            _ => throw new InvalidOperationException($"Unsupported verb: {ep.Method}")
        };

        var requestType = ep.RequestType.ToDisplayString();
        var isGet = ep.Method == HttpVerb.Get;
        var routeParams = ExposeToHttpEndpointHelpers.ExtractRouteParameters(ep.Route);
        var hasRouteParams = routeParams.Count > 0;

        // Case 1: GET — always use [AsParameters], minimal API handles route + query binding
        // Case 2: Non-GET, no route params — use [FromBody] directly
        // Case 3: Non-GET, with route params — bind route params individually + optional [FromBody] body DTO + construct request
        if (isGet || !hasRouteParams)
        {
            var fromAttr = isGet ? "[global::Microsoft.AspNetCore.Http.AsParametersAttribute]" : "[global::Microsoft.AspNetCore.Mvc.FromBodyAttribute]";
            sb.Append($"            app.{mapMethod}(\"{ep.Route}\",");
            sb.AppendLine();
            sb.AppendLine($"                async ({fromAttr} {requestType} request, [global::Microsoft.AspNetCore.Mvc.FromServicesAttribute] global::NOF.Contract.IRequestSender sender) =>");
            sb.AppendLine("                {");
            sb.AppendLine("                    var response = await sender.SendAsync(request);");
            sb.AppendLine("                    return global::Microsoft.AspNetCore.Http.TypedResults.Ok(response);");
            sb.Append("                })");
        }
        else
        {
            // Non-GET with route params: use generated Body DTO for OpenAPI-friendly binding
            var allProperties = ExposeToHttpEndpointHelpers.GetAllPublicProperties(ep.RequestType);
            var (routeParamProps, bodyProps) = SplitRouteAndBodyProps(allProperties, routeParams);
            var hasBody = bodyProps.Count > 0;
            var bodyDtoName = GetBodyDtoName(ep.RequestType);

            // Build lambda parameter list
            var lambdaParams = new List<string>();
            foreach (var (_, prop) in routeParamProps)
            {
                lambdaParams.Add($"{prop.Type.ToDisplayString()} {ToCamelCase(prop.Name)}");
            }
            if (hasBody)
            {
                lambdaParams.Add($"[global::Microsoft.AspNetCore.Mvc.FromBodyAttribute] {bodyDtoName} __body__");
            }
            lambdaParams.Add("[global::Microsoft.AspNetCore.Mvc.FromServicesAttribute] global::NOF.Contract.IRequestSender sender");

            var lambdaParamStr = string.Join(", ", lambdaParams);

            sb.Append($"            app.{mapMethod}(\"{ep.Route}\",");
            sb.AppendLine();
            sb.AppendLine($"                async ({lambdaParamStr}) =>");
            sb.AppendLine("                {");

            // Construct the request object
            // Supports three patterns:
            //   1. Record with primary ctor covering all props: new Request(a, b, c)
            //   2. Hybrid: ctor + extra settable props: new Request(a) { B = b, C = c }
            //   3. Parameterless ctor + object initializer: new Request { A = a, B = b }
            var bestCtor = FindBestConstructor(ep.RequestType, allProperties);
            var ctorParamNames = bestCtor != null
                ? new HashSet<string>(bestCtor.Parameters.Select(p => p.Name), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Build constructor arguments
            var ctorArgStr = "";
            if (bestCtor != null)
            {
                var ctorArgs = new List<string>();
                foreach (var ctorParam in bestCtor.Parameters)
                {
                    var routeMatch = routeParamProps.FirstOrDefault(rp =>
                        string.Equals(rp.Property.Name, ctorParam.Name, StringComparison.OrdinalIgnoreCase));
                    if (routeMatch.Property != null)
                    {
                        ctorArgs.Add(ToCamelCase(routeMatch.Property.Name));
                    }
                    else
                    {
                        var bodyMatch = bodyProps.FirstOrDefault(bp =>
                            string.Equals(bp.Name, ctorParam.Name, StringComparison.OrdinalIgnoreCase));
                        ctorArgs.Add(bodyMatch != null ? $"__body__.{bodyMatch.Name}" : "default");
                    }
                }
                ctorArgStr = string.Join(", ", ctorArgs);
            }

            // Collect remaining properties not covered by the constructor
            var initLines = new List<string>();
            foreach (var (_, prop) in routeParamProps)
            {
                if (!ctorParamNames.Contains(prop.Name))
                {
                    initLines.Add($"                        {prop.Name} = {ToCamelCase(prop.Name)}");
                }
            }
            foreach (var prop in bodyProps)
            {
                if (!ctorParamNames.Contains(prop.Name))
                {
                    initLines.Add($"                        {prop.Name} = __body__.{prop.Name}");
                }
            }

            // Emit: new RequestType(ctorArgs) { ExtraProp = value, ... };
            if (initLines.Count > 0)
            {
                sb.AppendLine($"                    var request = new {requestType}({ctorArgStr})");
                sb.AppendLine("                    {");
                for (var i = 0; i < initLines.Count; i++)
                {
                    sb.Append(initLines[i]);
                    sb.AppendLine(i < initLines.Count - 1 ? "," : "");
                }
                sb.AppendLine("                    };");
            }
            else
            {
                sb.AppendLine($"                    var request = new {requestType}({ctorArgStr});");
            }

            sb.AppendLine("                    var response = await sender.SendAsync(request);");
            sb.AppendLine("                    return global::Microsoft.AspNetCore.Http.TypedResults.Ok(response);");
            sb.Append("                })");
        }

        // Append OpenAPI metadata
        if (!string.IsNullOrEmpty(ep.DisplayName))
        {
            sb.Append($".WithName(\"{EscapeString(ep.DisplayName)}\")");
        }

        if (!string.IsNullOrEmpty(ep.Summary))
        {
            sb.Append($".WithSummary(\"{EscapeString(ep.Summary)}\")");
        }

        if (!string.IsNullOrEmpty(ep.Description))
        {
            sb.Append($".WithDescription(\"{EscapeString(ep.Description)}\")");
        }

        if (ep.Tags.Length > 0)
        {
            var tagArgs = string.Join(", ", ep.Tags.Select(t => $"\"{EscapeString(t)}\""));
            sb.Append($".WithTags({tagArgs})");
        }

        sb.AppendLine(";");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits a Body DTO class for a non-GET endpoint with route params that has body properties.
    /// The DTO contains only the non-route properties, giving OpenAPI full schema visibility.
    /// </summary>
    private static void EmitBodyDtoIfNeeded(StringBuilder sb, EndpointInfo ep)
    {
        if (ep.Method == HttpVerb.Get)
        {
            return;
        }

        var routeParams = ExposeToHttpEndpointHelpers.ExtractRouteParameters(ep.Route);
        if (routeParams.Count == 0)
        {
            return;
        }

        var allProperties = ExposeToHttpEndpointHelpers.GetAllPublicProperties(ep.RequestType);
        var (_, bodyProps) = SplitRouteAndBodyProps(allProperties, routeParams);
        if (bodyProps.Count == 0)
        {
            return;
        }

        var dtoName = GetBodyDtoName(ep.RequestType);
        sb.AppendLine($"    public class {dtoName}");
        sb.AppendLine("    {");
        foreach (var prop in bodyProps)
        {
            var propType = prop.Type.ToDisplayString();
            var defaultValue = prop.Type.IsValueType ? "" : " = default!;";
            if (prop.Type.NullableAnnotation == NullableAnnotation.Annotated)
            {
                defaultValue = "";
            }

            sb.AppendLine($"        public {propType} {prop.Name} {{ get; set; }}{defaultValue}");
        }
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Find a constructor whose parameters match all properties (by name, case-insensitive).
    /// Prefers the constructor with the most parameters (primary ctor for records).
    /// </summary>
    private static IMethodSymbol FindBestConstructor(INamedTypeSymbol typeSymbol, List<IPropertySymbol> allProperties)
    {
        var propNames = new HashSet<string>(allProperties.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

        return typeSymbol.Constructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public
                        && !c.IsStatic
                        && c.Parameters.Length > 0
                        && c.Parameters.All(p => propNames.Contains(p.Name)))
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault();
    }

    private static (List<(string RouteParamName, IPropertySymbol Property)> RouteProps, List<IPropertySymbol> BodyProps)
        SplitRouteAndBodyProps(List<IPropertySymbol> allProperties, List<string> routeParams)
    {
        var routeParamProps = new List<(string RouteParamName, IPropertySymbol Property)>();
        var bodyProps = new List<IPropertySymbol>();

        foreach (var prop in allProperties)
        {
            var matchedParam = routeParams.FirstOrDefault(rp =>
                string.Equals(rp, prop.Name, StringComparison.OrdinalIgnoreCase));
            if (matchedParam != null)
            {
                routeParamProps.Add((matchedParam, prop));
            }
            else
            {
                bodyProps.Add(prop);
            }
        }

        return (routeParamProps, bodyProps);
    }

    private static string GetBodyDtoName(INamedTypeSymbol requestType)
        => $"__{requestType.Name}_Body__";

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static string EscapeString(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
