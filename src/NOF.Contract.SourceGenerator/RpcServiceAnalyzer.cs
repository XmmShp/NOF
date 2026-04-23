using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.Contract.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RpcServiceAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor RequestMustBeReferenceType = new(
        "NOF200",
        "Request must be a reference type",
        "Request type '{0}' must be a class or record, not a struct",
        "HttpEndpoint",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor RouteParametersNotSupported = new(
        "NOF201",
        "Route parameters are not supported",
        "HttpEndpoint route '{0}' must not contain route parameters. Put input data on the request object instead.",
        "HttpEndpoint",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor ClassMustHaveParameterlessCtor = new(
        "NOF202",
        "Class request must have a parameterless constructor",
        "Request class '{0}' must have a public parameterless constructor. Records may use primary constructors, but classes must have a parameterless constructor.",
        "HttpEndpoint",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor InvalidServiceMethodSignature = new(
        "NOF207",
        "Invalid service method signature",
        "Method '{0}' on service interface '{1}' must have exactly 1 request parameter, must not declare CancellationToken, must not end with Async, and must return a non-Task value",
        "RpcService",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor ServiceMethodOverloadsNotSupported = new(
        "NOF208",
        "Service method overloading is not supported",
        "Service interface '{1}' contains overloaded methods named '{0}'. Overloading is not supported; use unique method names.",
        "RpcService",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor VoidReturnNotSupported = new(
        "NOF209",
        "Void return is not supported",
        "Method '{0}' on service interface '{1}' must not return void. Return a concrete response type instead, such as Empty when no payload is needed.",
        "RpcService",
        DiagnosticSeverity.Error,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        RequestMustBeReferenceType,
        RouteParametersNotSupported,
        ClassMustHaveParameterlessCtor,
        InvalidServiceMethodSignature,
        ServiceMethodOverloadsNotSupported,
        VoidReturnNotSupported
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        AnalyzeLegacyHttpEndpointAttributes(context, typeSymbol);
        AnalyzeRpcServiceInterface(context, typeSymbol);
    }

    private static void AnalyzeLegacyHttpEndpointAttributes(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol)
    {
        var httpEndpointAttributes = typeSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString() == RpcServiceHelpers.HttpEndpointAttributeFqn)
            .ToList();

        if (httpEndpointAttributes.Count == 0)
        {
            return;
        }

        var typeLocation = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
        ValidateRequestPayloadShape(context, typeSymbol, typeLocation);

        foreach (var attr in httpEndpointAttributes)
        {
            var attrLocation = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? typeLocation;
            var route = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1].Value as string : null;
            if (string.IsNullOrWhiteSpace(route))
            {
                continue;
            }

            ValidateRoute(context, route!, attrLocation);
        }
    }

    private static void AnalyzeRpcServiceInterface(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol)
    {
        if (!RpcServiceHelpers.IsRpcServiceInterface(typeSymbol))
        {
            return;
        }
        var attrLocation = typeSymbol.Locations.FirstOrDefault() ?? Location.None;

        // 禁止同名重载：按名称分组，若某一名称出现多次则全部报错
        var declaredMethods = typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared)
            .ToList();
        foreach (var group in declaredMethods.GroupBy(m => m.Name, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                foreach (var overloaded in group)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(ServiceMethodOverloadsNotSupported,
                                          overloaded.Locations.FirstOrDefault() ?? attrLocation,
                                          overloaded.Name, typeSymbol.Name));
                }
            }
        }

        foreach (var method in declaredMethods)
        {
            if (method.ReturnsVoid)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(VoidReturnNotSupported, method.Locations.FirstOrDefault() ?? attrLocation, method.Name, typeSymbol.Name));
                continue;
            }

            var validRequestParameter = RpcServiceHelpers.TryGetRequestParameter(method, out var requestParameter);
            var validReturnType = RpcServiceHelpers.TryGetServiceReturnInfo(method, out _);
            var validMethodName = !method.Name.EndsWith("Async", StringComparison.Ordinal);
            if (!validRequestParameter || !validReturnType || !validMethodName)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(InvalidServiceMethodSignature, method.Locations.FirstOrDefault() ?? attrLocation, method.Name, typeSymbol.Name));
                continue;
            }

            INamedTypeSymbol? requestType = null;
            if (requestParameter != null)
            {
                if (requestParameter.Type is not INamedTypeSymbol type)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(InvalidServiceMethodSignature, method.Locations.FirstOrDefault() ?? attrLocation, method.Name, typeSymbol.Name));
                    continue;
                }
                requestType = type;
            }

            var methodHttpEndpointAttr = method.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == RpcServiceHelpers.HttpEndpointAttributeFqn);
            if (methodHttpEndpointAttr is null)
            {
                continue;
            }

            var methodLocation = method.Locations.FirstOrDefault() ?? attrLocation;
            if (requestType != null)
            {
                ValidateRequestPayloadShape(context, requestType, methodLocation);

                var route = methodHttpEndpointAttr.ConstructorArguments.Length > 1
                    ? methodHttpEndpointAttr.ConstructorArguments[1].Value as string
                    : null;
                if (string.IsNullOrWhiteSpace(route))
                {
                    continue;
                }

                ValidateRoute(context, route!, methodLocation);
            }
        }

    }

    private static void ValidateRequestPayloadShape(SymbolAnalysisContext context, INamedTypeSymbol requestType, Location location)
    {
        if (requestType.IsValueType)
        {
            context.ReportDiagnostic(Diagnostic.Create(RequestMustBeReferenceType, location, requestType.Name));
            return;
        }

        if (requestType.IsRecord)
        {
            return;
        }

        var hasParameterlessCtor = requestType.Constructors
            .Any(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic && c.Parameters.Length == 0);

        var hasExplicitCtors = requestType.Constructors
            .Any(c => !c.IsStatic && !c.IsImplicitlyDeclared);

        if (hasExplicitCtors && !hasParameterlessCtor)
        {
            context.ReportDiagnostic(Diagnostic.Create(ClassMustHaveParameterlessCtor, location, requestType.Name));
        }
    }

    private static void ValidateRoute(
        SymbolAnalysisContext context,
        string route,
        Location location)
    {
        if (RpcServiceHelpers.ContainsRouteParameters(route))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(RouteParametersNotSupported, location, route));
        }
    }

}
