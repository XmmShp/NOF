using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF.Contract.SourceGenerator;

[Generator]
public class ExposeToHttpEndpointServiceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find partial interfaces with [GenerateService]
        var interfaceProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is InterfaceDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, _) =>
                {
                    if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is INamedTypeSymbol symbol
                        && ExposeToHttpEndpointHelpers.HasGenerateServiceAttribute(symbol))
                    {
                        return symbol;
                    }
                    return null;
                })
            .Where(static m => m is not null);

        var compilationAndInterfaces = context.CompilationProvider.Combine(interfaceProvider.Collect());
        context.RegisterSourceOutput(compilationAndInterfaces, Generate);
    }

    private static void Generate(SourceProductionContext context, (Compilation Compilation, ImmutableArray<INamedTypeSymbol?> Interfaces) source)
    {
        if (source.Interfaces.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var iface in source.Interfaces.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            GenerateForInterface(context, source.Compilation, iface);
        }
    }

    private static void GenerateForInterface(SourceProductionContext context, Compilation compilation, INamedTypeSymbol iface)
    {
        var attr = iface.GetAttributes()
            .First(a => a.AttributeClass?.ToDisplayString() == ExposeToHttpEndpointHelpers.GenerateServiceAttributeFqn);

        // Parse attribute parameters
        var namespacesArg = attr.NamedArguments
            .FirstOrDefault(a => a.Key == "Namespaces").Value;
        var generateHttp = attr.NamedArguments
            .FirstOrDefault(a => a.Key == "GenerateHttpClient").Value.Value is not false; // default true
        var generateRequestSender = attr.NamedArguments
            .FirstOrDefault(a => a.Key == "GenerateRequestSenderClient").Value.Value is not false; // default true
        var extraTypesArg = attr.NamedArguments
            .FirstOrDefault(a => a.Key == "ExtraTypes").Value;

        // Determine scan namespaces
        var scanNamespaces = new HashSet<string>(StringComparer.Ordinal);
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
            // Default: same namespace as the interface
            var ifaceNs = ExposeToHttpEndpointHelpers.GetFullNamespace(iface.ContainingNamespace);
            if (!string.IsNullOrEmpty(ifaceNs))
            {
                scanNamespaces.Add(ifaceNs);
            }
        }

        // Collect extra types from attribute
        var extraTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
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

        // Scan for PublicApi request types in the specified namespaces
        var publicApiInfos = new List<PublicApiInfo>();
        var seenTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        CollectPublicApiTypes(compilation.Assembly.GlobalNamespace, scanNamespaces, publicApiInfos, seenTypes);

        // Also scan referenced assemblies
        foreach (var refAsm in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            CollectPublicApiTypes(refAsm.GlobalNamespace, scanNamespaces, publicApiInfos, seenTypes);
        }

        // Add extra types
        foreach (var extra in extraTypes)
        {
            if (seenTypes.Add(extra) && ExposeToHttpEndpointHelpers.HasPublicApiAttribute(extra)
                                     && ExposeToHttpEndpointHelpers.IsRequestType(extra))
            {
                publicApiInfos.Add(ExposeToHttpEndpointHelpers.ExtractPublicApiInfo(extra));
            }
        }

        if (publicApiInfos.Count == 0)
        {
            return;
        }

        // Check which methods already exist on the partial interface (user-defined)
        var existingMethods = GetExistingMethodSignatures(iface);

        var interfaceName = iface.Name;
        var ifaceNamespace = ExposeToHttpEndpointHelpers.GetFullNamespace(iface.ContainingNamespace);
        var httpClientName = ExposeToHttpEndpointHelpers.GetHttpClientName(interfaceName);
        var requestSenderClientName = ExposeToHttpEndpointHelpers.GetRequestSenderClientName(interfaceName);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using NOF.Contract;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ifaceNamespace}");
        sb.AppendLine("{");

        // Generate interface methods
        sb.AppendLine($"    public partial interface {interfaceName}");
        sb.AppendLine("    {");

        var methodsToGenerate = new List<PublicApiInfo>();
        foreach (var api in publicApiInfos)
        {
            var methodName = api.OperationName + "Async";
            var requestType = api.RequestType.ToDisplayString();

            // Check for signature conflict: same method name and first param type
            var sig = $"{methodName}({requestType})";
            if (existingMethods.Contains(sig))
            {
                continue;
            }

            methodsToGenerate.Add(api);

            var responseType = api.ResponseType?.ToDisplayString();
            var returnType = string.IsNullOrEmpty(responseType) ? "global::NOF.Contract.Result" : $"global::NOF.Contract.Result<{responseType}>";

            sb.AppendLine("        /// <summary>");
            if (!string.IsNullOrEmpty(api.DisplayName))
            {
                sb.AppendLine($"        /// {EscapeXmlComment(api.DisplayName)}");
            }
            else
            {
                sb.AppendLine($"        /// Calls {api.OperationName} operation");
            }
            if (!string.IsNullOrEmpty(api.Summary))
            {
                sb.AppendLine($"        /// <para>{EscapeXmlComment(api.Summary)}</para>");
            }
            if (!string.IsNullOrEmpty(api.Description))
            {
                sb.AppendLine($"        /// <para>{EscapeXmlComment(api.Description)}</para>");
            }
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"request\">Request parameters</param>");
            sb.AppendLine("        /// <param name=\"cancellationToken\">Cancellation token</param>");
            sb.AppendLine($"        global::System.Threading.Tasks.Task<{returnType}> {methodName}({requestType} request, global::System.Threading.CancellationToken cancellationToken = default);");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate HTTP client implementation
        if (generateHttp)
        {
            EmitHttpClient(sb, interfaceName, httpClientName, methodsToGenerate, compilation);
        }

        // Generate RequestSender client implementation
        if (generateRequestSender)
        {
            EmitRequestSenderClient(sb, interfaceName, requestSenderClientName, methodsToGenerate);
        }

        sb.AppendLine("}");

        context.AddSource($"{ifaceNamespace}.{interfaceName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void EmitHttpClient(StringBuilder sb, string interfaceName, string clientName,
        List<PublicApiInfo> methods, Compilation compilation)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// HTTP client implementation of {interfaceName}");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public partial class {clientName} : {interfaceName}");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly global::System.Net.Http.HttpClient _httpClient;");
        sb.AppendLine("        private static readonly global::System.Text.Json.JsonSerializerOptions _jsonOptions = global::System.Text.Json.JsonSerializerOptions.NOF;");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Initializes a new instance of {clientName}");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"httpClient\">HTTP client</param>");
        sb.AppendLine($"        public {clientName}(global::System.Net.Http.HttpClient httpClient)");
        sb.AppendLine("        {");
        sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(httpClient);");
        sb.AppendLine("            _httpClient = httpClient;");
        sb.AppendLine("        }");
        sb.AppendLine();

        foreach (var api in methods)
        {
            EndpointInfo endpointInfo;
            if (ExposeToHttpEndpointHelpers.HasHttpEndpointAttribute(api.RequestType))
            {
                var httpAttr = api.RequestType.GetAttributes()
                    .First(a => a.AttributeClass?.ToDisplayString() == ExposeToHttpEndpointHelpers.HttpEndpointAttributeFqn);
                endpointInfo = ExposeToHttpEndpointHelpers.ExtractEndpointInfo(api.RequestType, httpAttr);
            }
            else
            {
                // No [HttpEndpoint] — default to POST with operation name as route
                endpointInfo = ExposeToHttpEndpointHelpers.ExtractDefaultEndpointInfo(api.RequestType);
            }
            EmitHttpMethodBody(sb, endpointInfo);
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitRequestSenderClient(StringBuilder sb, string interfaceName, string clientName,
        List<PublicApiInfo> methods)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// In-process RequestSender implementation of {interfaceName}");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public partial class {clientName} : {interfaceName}");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly global::NOF.Contract.IRequestSender _requestSender;");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Initializes a new instance of {clientName}");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"requestSender\">Request sender</param>");
        sb.AppendLine($"        public {clientName}(global::NOF.Contract.IRequestSender requestSender)");
        sb.AppendLine("        {");
        sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(requestSender);");
        sb.AppendLine("            _requestSender = requestSender;");
        sb.AppendLine("        }");
        sb.AppendLine();

        foreach (var api in methods)
        {
            EmitRequestSenderMethodBody(sb, api);
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitRequestSenderMethodBody(StringBuilder sb, PublicApiInfo api)
    {
        var requestType = api.RequestType.ToDisplayString();
        var responseType = api.ResponseType?.ToDisplayString();
        var returnType = string.IsNullOrEmpty(responseType) ? "global::NOF.Contract.Result" : $"global::NOF.Contract.Result<{responseType}>";
        var methodName = api.OperationName;

        sb.AppendLine("        /// <inheritdoc />");
        sb.AppendLine($"        public virtual async global::System.Threading.Tasks.Task<{returnType}> {methodName}Async({requestType} request, global::System.Threading.CancellationToken cancellationToken)");
        sb.AppendLine("        {");
        sb.AppendLine($"            return await _requestSender.SendAsync(request, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void EmitHttpMethodBody(StringBuilder sb, EndpointInfo endpoint)
    {
        var requestType = endpoint.RequestType.ToDisplayString();
        var responseType = endpoint.ResponseType?.ToDisplayString();
        var returnType = string.IsNullOrEmpty(responseType) ? "global::NOF.Contract.Result" : $"global::NOF.Contract.Result<{responseType}>";
        var methodName = endpoint.OperationName;
        var httpMethod = GetHttpMethod(endpoint.Method);
        var isBodyMethod = IsBodyMethod(endpoint.Method);
        var fqnHttpMethod = $"global::System.Net.Http.{httpMethod}";

        var allProperties = ExposeToHttpEndpointHelpers.GetAllPublicProperties(endpoint.RequestType);
        var routeParams = ExposeToHttpEndpointHelpers.ExtractRouteParameters(endpoint.Route);

        var routeParamProperties = new List<(string ParamName, IPropertySymbol Property)>();
        var nonRouteProperties = new List<IPropertySymbol>();

        foreach (var prop in allProperties)
        {
            var matchedParam = routeParams.FirstOrDefault(rp =>
                string.Equals(rp, prop.Name, StringComparison.OrdinalIgnoreCase));
            if (matchedParam != null)
            {
                routeParamProperties.Add((matchedParam, prop));
            }
            else
            {
                nonRouteProperties.Add(prop);
            }
        }

        var hasRouteParams = routeParamProperties.Count > 0;

        sb.AppendLine("        /// <inheritdoc />");
        sb.AppendLine($"        public virtual async global::System.Threading.Tasks.Task<{returnType}> {methodName}Async({requestType} request, global::System.Threading.CancellationToken cancellationToken)");
        sb.AppendLine("        {");

        if (hasRouteParams)
        {
            var routeExpression = BuildRouteInterpolation(sb, endpoint.Route, routeParamProperties);
            sb.AppendLine($"            var endpoint = {routeExpression};");
        }
        else
        {
            sb.AppendLine($"            var endpoint = \"{endpoint.Route}\";");
        }

        if (isBodyMethod)
        {
            if (hasRouteParams && nonRouteProperties.Count > 0)
            {
                sb.AppendLine($"            var body = new global::System.Collections.Generic.Dictionary<string, object?>({nonRouteProperties.Count});");
                foreach (var prop in nonRouteProperties)
                {
                    sb.AppendLine($"            body[\"{prop.Name}\"] = request.{prop.Name};");
                }
                sb.AppendLine("            using var httpRequest = new global::System.Net.Http.HttpRequestMessage(" + fqnHttpMethod + ", endpoint);");
                sb.AppendLine("            httpRequest.Content = global::System.Net.Http.Json.JsonContent.Create(body, options: _jsonOptions);");
            }
            else if (hasRouteParams && nonRouteProperties.Count == 0)
            {
                sb.AppendLine("            using var httpRequest = new global::System.Net.Http.HttpRequestMessage(" + fqnHttpMethod + ", endpoint);");
            }
            else
            {
                sb.AppendLine("            using var httpRequest = new global::System.Net.Http.HttpRequestMessage(" + fqnHttpMethod + ", endpoint);");
                sb.AppendLine($"            httpRequest.Content = global::System.Net.Http.Json.JsonContent.Create(request, typeof({requestType}), options: _jsonOptions);");
            }
        }
        else
        {
            if (nonRouteProperties.Count > 0)
            {
                sb.AppendLine("            var queryParts = new global::System.Collections.Generic.List<string>();");
                foreach (var prop in nonRouteProperties)
                {
                    EmitQueryParamAppend(sb, prop);
                }
                sb.AppendLine("            if (queryParts.Count > 0)");
                sb.AppendLine("            {");
                sb.AppendLine("                endpoint = endpoint + \"?\" + string.Join(\"&\", queryParts);");
                sb.AppendLine("            }");
            }
            sb.AppendLine("            using var httpRequest = new global::System.Net.Http.HttpRequestMessage(" + fqnHttpMethod + ", endpoint);");
        }

        sb.AppendLine("            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);");
        EmitResponseHandling(sb, responseType);
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void EmitResponseHandling(StringBuilder sb, string? responseType)
    {
        sb.AppendLine("            if (!response.IsSuccessStatusCode)");
        sb.AppendLine("            {");
        sb.AppendLine("                return global::NOF.Contract.Result.Fail(((int)response.StatusCode).ToString(), $\"{(int)response.StatusCode}: {response.ReasonPhrase}\");");
        sb.AppendLine("            }");
        sb.AppendLine("            try");
        sb.AppendLine("            {");

        if (string.IsNullOrEmpty(responseType))
        {
            sb.AppendLine("                var apiResponse = await global::System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<global::NOF.Contract.Result>(response.Content, _jsonOptions, cancellationToken);");
            sb.AppendLine("                return apiResponse ?? global::NOF.Contract.Result.Fail(\"500\", \"Unexpected null response from server.\");");
        }
        else
        {
            sb.AppendLine($"                var apiResponse = await global::System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<global::NOF.Contract.Result<{responseType}>>(response.Content, _jsonOptions, cancellationToken);");
            sb.AppendLine("                return apiResponse ?? global::NOF.Contract.Result.Fail(\"500\", \"Unexpected null response from server.\");");
        }

        sb.AppendLine("            }");
        sb.AppendLine("            catch (global::System.Text.Json.JsonException ex)");
        sb.AppendLine("            {");
        sb.AppendLine("                return global::NOF.Contract.Result.Fail(\"400\", $\"Response deserialization failed: {ex.Message}\");");
        sb.AppendLine("            }");
        sb.AppendLine();
    }

    private static void CollectPublicApiTypes(INamespaceSymbol ns, HashSet<string> scanNamespaces,
        List<PublicApiInfo> results, HashSet<INamedTypeSymbol> seen)
    {
        var currentNs = ExposeToHttpEndpointHelpers.GetFullNamespace(ns);

        if (!string.IsNullOrEmpty(currentNs) && scanNamespaces.Any(scan => currentNs.StartsWith(scan)))
        {
            foreach (var type in ns.GetTypeMembers())
            {
                if (type is { DeclaredAccessibility: Accessibility.Public, IsAbstract: false }
                    && ExposeToHttpEndpointHelpers.HasPublicApiAttribute(type)
                    && ExposeToHttpEndpointHelpers.IsRequestType(type)
                    && seen.Add(type))
                {
                    results.Add(ExposeToHttpEndpointHelpers.ExtractPublicApiInfo(type));
                }
            }
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            CollectPublicApiTypes(child, scanNamespaces, results, seen);
        }
    }

    /// <summary>
    /// Gets signatures of methods already defined on the interface (by user in partial declarations).
    /// Signature format: "MethodNameAsync(Full.Type.Name)"
    /// </summary>
    private static HashSet<string> GetExistingMethodSignatures(INamedTypeSymbol iface)
    {
        var sigs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in iface.GetMembers())
        {
            if (member is IMethodSymbol method && !method.IsImplicitlyDeclared)
            {
                var firstParam = method.Parameters.FirstOrDefault();
                var paramType = firstParam?.Type.ToDisplayString() ?? "";
                sigs.Add($"{method.Name}({paramType})");
            }
        }
        return sigs;
    }

    private static void EmitQueryParamAppend(StringBuilder sb, IPropertySymbol prop)
    {
        var propName = prop.Name;
        var isNullable = prop.Type.NullableAnnotation == NullableAnnotation.Annotated
                         || prop.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

        if (isNullable)
        {
            sb.AppendLine($"            if (request.{propName} is not null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                queryParts.Add(global::System.Uri.EscapeDataString(\"{propName}\") + \"=\" + global::System.Uri.EscapeDataString({FormatValueExpression($"request.{propName}", prop.Type, true)}));");
            sb.AppendLine("            }");
        }
        else
        {
            sb.AppendLine($"            queryParts.Add(global::System.Uri.EscapeDataString(\"{propName}\") + \"=\" + global::System.Uri.EscapeDataString({FormatValueExpression($"request.{propName}", prop.Type, false)}));");
        }
    }

    internal static string FormatValueExpression(string accessor, ITypeSymbol type, bool isNullable)
    {
        var underlying = type;
        var isNullableValueType = false;
        if (isNullable && type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } namedType)
        {
            underlying = namedType.TypeArguments[0];
            isNullableValueType = true;
        }

        var display = underlying.ToDisplayString();
        var valueAccessor = isNullableValueType ? $"{accessor}.Value" : accessor;

        return display switch
        {
            "System.DateTime" => $"{valueAccessor}.ToString(\"O\", global::System.Globalization.CultureInfo.InvariantCulture)",
            "System.DateTimeOffset" => $"{valueAccessor}.ToString(\"O\", global::System.Globalization.CultureInfo.InvariantCulture)",
            "System.DateOnly" => $"{valueAccessor}.ToString(\"yyyy-MM-dd\", global::System.Globalization.CultureInfo.InvariantCulture)",
            "System.TimeOnly" => $"{valueAccessor}.ToString(\"HH:mm:ss.FFFFFFF\", global::System.Globalization.CultureInfo.InvariantCulture)",
            "string" => isNullable ? $"{accessor}!" : accessor,
            _ => $"{accessor}.ToString()!"
        };
    }

    private static string BuildRouteInterpolation(StringBuilder sb, string route, List<(string ParamName, IPropertySymbol Property)> routeParamProperties)
    {
        var result = route;
        foreach (var (paramName, prop) in routeParamProperties)
        {
            var localVarName = $"__route_{paramName}__";
            var valueExpr = FormatValueExpression($"request.{prop.Name}", prop.Type, false);
            sb.AppendLine($"            var {localVarName} = global::System.Uri.EscapeDataString({valueExpr});");

            var pattern = "{" + paramName + "}";
            var replacement = $"{{{localVarName}}}";
            var idx = result.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                result = result.Substring(0, idx) + replacement + result.Substring(idx + pattern.Length);
            }
        }
        return "$\"" + result + "\"";
    }

    private static string GetHttpMethod(HttpVerb verb) => verb switch
    {
        HttpVerb.Get => "HttpMethod.Get",
        HttpVerb.Post => "HttpMethod.Post",
        HttpVerb.Put => "HttpMethod.Put",
        HttpVerb.Delete => "HttpMethod.Delete",
        HttpVerb.Patch => "HttpMethod.Patch",
        _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, null)
    };

    private static bool IsBodyMethod(HttpVerb verb) =>
        verb == HttpVerb.Post || verb == HttpVerb.Put || verb == HttpVerb.Patch;

    private static string EscapeXmlComment(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
