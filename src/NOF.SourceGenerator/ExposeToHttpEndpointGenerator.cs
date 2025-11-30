using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF;

[Generator]
public class ExposeToHttpEndpointGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all classes with ExposeToHttpEndpointAttribute
        var provider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, _) => GetClassDeclarationWithEndpointAttribute(ctx))
            .Where(static m => m is not null);

        var compilationAndClasses = context.CompilationProvider.Combine(provider.Collect());
        context.RegisterSourceOutput(compilationAndClasses, GenerateCode);
    }

    private static TypeDeclarationSyntax? GetClassDeclarationWithEndpointAttribute(GeneratorSyntaxContext context)
    {
        var classDecl = (TypeDeclarationSyntax)context.Node;
        var model = context.SemanticModel;
        var classSymbol = model.GetDeclaredSymbol(classDecl);

        if (classSymbol is null)
        {
            return null;
        }

        // Check for ExposeToHttpEndpointAttribute
        var hasEndpointAttribute = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() == "NOF.ExposeToHttpEndpointAttribute");

        return hasEndpointAttribute && IsRequestType(classSymbol)
            ? classDecl
            : null;
    }

    private static bool IsRequestType(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(i =>
            i.ToDisplayString() == "NOF.IRequest"
            || (i is { IsGenericType: true, ConstructedFrom.SpecialType: SpecialType.None }
                && i.OriginalDefinition.ToDisplayString() == "NOF.IRequest<TResponse>"));
    }

    private static void GenerateCode(SourceProductionContext context, (Compilation Compilation, ImmutableArray<TypeDeclarationSyntax?> Classes) source)
    {
        if (source.Classes.IsDefaultOrEmpty)
        {
            return;
        }

        var endpointsByNamespace = new Dictionary<string, List<EndpointInfo>>();

        foreach (var classDecl in source.Classes.Distinct().OfType<TypeDeclarationSyntax>())
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var model = source.Compilation.GetSemanticModel(classDecl.SyntaxTree);
            var classSymbol = model.GetDeclaredSymbol(classDecl);

            if (classSymbol is null)
            {
                continue;
            }

            var attributes = classSymbol.GetAttributes()
                .Where(attr => attr.AttributeClass?.ToDisplayString() == "NOF.ExposeToHttpEndpointAttribute")
                .ToList();

            if (attributes.Count == 0)
            {
                continue;
            }

            // Get root namespace for service name
            var rootNamespace = GetRootNamespace(classSymbol.ContainingNamespace);
            if (string.IsNullOrEmpty(rootNamespace))
            {
                continue;
            }

            // Get or create endpoint list for this namespace
            if (!endpointsByNamespace.TryGetValue(rootNamespace!, out var endpoints))
            {
                endpoints = [];
                endpointsByNamespace[rootNamespace!] = endpoints;
            }

            // Process each endpoint attribute
            foreach (var attr in attributes)
            {
                var method = (HttpVerb)attr.ConstructorArguments[0].Value!;
                var route = attr.ConstructorArguments.Length > 1
                    ? attr.ConstructorArguments[1].Value as string
                    : null;
                route = route?.TrimEnd('/');

                // Get operation name from attribute or derive from type name
                var operationName = attr.NamedArguments
                    .FirstOrDefault(arg => arg.Key == "OperationName").Value.Value as string;

                const string requestSuffix = "Request";
                operationName ??= classSymbol.Name.EndsWith(requestSuffix)
                    ? classSymbol.Name.Substring(0, classSymbol.Name.Length - requestSuffix.Length)
                    : classSymbol.Name;

                // If route not specified, use operation name
                route ??= operationName;

                // Get response type
                var responseType = GetResponseType(classSymbol);

                // Get permission and anonymous settings
                var permission = attr.NamedArguments
                    .FirstOrDefault(arg => arg.Key == "Permission").Value.Value as string;
                var allowAnonymous = attr.NamedArguments
                    .FirstOrDefault(arg => arg.Key == "AllowAnonymous").Value.Value is true;

                endpoints.Add(new EndpointInfo
                {
                    RequestType = classSymbol,
                    ResponseType = responseType,
                    Method = method,
                    Route = route,
                    OperationName = operationName,
                    Permission = permission,
                    AllowAnonymous = allowAnonymous
                });
            }
        }

        foreach (var kv in endpointsByNamespace)
        {
            var namespaceName = kv.Key;
            var endpoints = kv.Value;
            GenerateForNamespace(context, namespaceName, endpoints);
        }
    }

    private static void GenerateForNamespace(SourceProductionContext context, string namespaceName, IEnumerable<EndpointInfo> endpoints)
    {
        var endpointsArray = endpoints.ToArray();
        if (endpointsArray.Length == 0)
        {
            return;
        }

        const string service = "Service";
        var serviceName = namespaceName.EndsWith(service) ? namespaceName : $"{namespaceName}Service";
        var interfaceName = $"I{serviceName}";
        var clientName = $"{serviceName}Client";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System.Net.Http;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using NOF;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// {namespaceName} service client interface");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public interface {interfaceName}");
        sb.AppendLine("    {");

        var sb2 = new StringBuilder();
        sb2.AppendLine("// <auto-generated/>");
        sb2.AppendLine("#nullable enable");
        sb2.AppendLine();
        sb2.AppendLine("using System;");
        sb2.AppendLine("using System.Collections.Immutable;");
        sb2.AppendLine("using NOF;");
        sb2.AppendLine();
        sb2.AppendLine($"namespace {namespaceName}");
        sb2.AppendLine("{");
        sb2.AppendLine("    /// <summary>");
        sb2.AppendLine("    /// Contains metadata about all HTTP endpoints in this assembly.");
        sb2.AppendLine("    /// This class is auto-generated and should not be modified manually.");
        sb2.AppendLine("    /// </summary>");
        sb2.AppendLine("    public static class HttpEndpoints");
        sb2.AppendLine("    {");
        sb2.AppendLine("        /// <summary>");
        sb2.AppendLine("        /// Gets an array of all HTTP endpoints in this assembly.");
        sb2.AppendLine("        /// </summary>");
        sb2.AppendLine("        public static ImmutableArray<HttpEndpoint> AllEndpoints { get; } = [");

        // Generate interface methods
        foreach (var endpoint in endpointsArray)
        {
            var requestType = endpoint.RequestType.ToDisplayString();
            var responseType = endpoint.ResponseType?.ToDisplayString();
            var returnType = string.IsNullOrEmpty(responseType) ? "NOF.Result" : $"NOF.Result<{responseType}>";
            var methodName = endpoint.OperationName;

            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Calls {endpoint.Route} endpoint");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"request\">Request parameters</param>");
            sb.AppendLine("        /// <returns>Task result</returns>");
            sb.AppendLine($"        Task<{returnType}> {methodName}Async({requestType} request);");
            sb.AppendLine();

            var permission = endpoint.Permission != null
                ? $"\"{endpoint.Permission}\""
                : "null";

            sb2.AppendLine($"            new(typeof({requestType}), HttpVerb.{endpoint.Method}, \"{endpoint.Route}\", {permission}, {endpoint.AllowAnonymous.ToString().ToLower()}),");
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate client implementation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// {namespaceName} service client implementation");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public class {clientName} : {interfaceName}");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly HttpClient _httpClient;");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Initializes a new instance of {clientName}");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"httpClient\">HTTP client</param>");
        sb.AppendLine($"        public {clientName}(HttpClient httpClient)");
        sb.AppendLine("        {");
        sb.AppendLine("            _httpClient = httpClient;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Generate client methods
        foreach (var endpoint in endpointsArray)
        {
            var requestType = endpoint.RequestType.ToDisplayString();
            var responseType = endpoint.ResponseType?.ToDisplayString();
            var returnType = string.IsNullOrEmpty(responseType) ? "NOF.Result" : $"NOF.Result<{responseType}>";
            var methodName = endpoint.OperationName;

            sb.AppendLine("        /// <inheritdoc />");
            sb.AppendLine($"        public async Task<{returnType}> {methodName}Async({requestType} request)");
            sb.AppendLine("        {");

            if (string.IsNullOrEmpty(responseType))
            {
                var operation = endpoint.Method == HttpVerb.Get
                    ? $"            return await _httpClient.SendGetRequestAsync($\"{endpoint.Route}{{request.ToQueryString()}}\");"
                    : $"            return await _httpClient.Send{endpoint.Method}RequestAsync(\"{endpoint.Route}\", request);";
                sb.AppendLine(operation);
            }
            else
            {
                var operation = endpoint.Method == HttpVerb.Get
                    ? $"            return await _httpClient.SendGetRequestAsync<{responseType}>($\"{endpoint.Route}{{request.ToQueryString()}}\");"
                    : $"            return await _httpClient.Send{endpoint.Method}RequestAsync<{responseType}>(\"{endpoint.Route}\", request);";
                sb.AppendLine(operation);
            }

            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb2.AppendLine("        ];");
        sb2.AppendLine("    }");
        sb2.AppendLine("}");
        // Add to source output
        context.AddSource($"{serviceName}Client.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        context.AddSource($"{serviceName}HttpEndpoints.g.cs", SourceText.From(sb2.ToString(), Encoding.UTF8));
    }

    private static string? GetRootNamespace(INamespaceSymbol ns)
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

    private static ITypeSymbol? GetResponseType(INamedTypeSymbol requestType)
    {
        var requestInterface = requestType.AllInterfaces
            .FirstOrDefault(i => i.OriginalDefinition.ToDisplayString() == "NOF.IRequest<TResponse>"
                                 && i.IsGenericType);

        return requestInterface is { TypeArguments.Length: 1 }
            ? requestInterface.TypeArguments[0]
            : null;
    }


}

internal class EndpointInfo
{
    // Request type information
    public INamedTypeSymbol RequestType { get; set; } = null!;
    public ITypeSymbol? ResponseType { get; set; }

    // Endpoint configuration
    public HttpVerb Method { get; set; }
    public string Route { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;

    // Security
    public string? Permission { get; set; }
    public bool AllowAnonymous { get; set; }
}

internal enum HttpVerb
{
    Get,
    Post,
    Put,
    Delete,
    Patch
}