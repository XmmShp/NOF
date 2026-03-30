using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.Contract.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ExposeToHttpEndpointAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor RequestMustBeReferenceType = new(
        "NOF200",
        "Request must be a reference type",
        "Request type '{0}' must be a class or record, not a struct",
        "HttpEndpoint",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor MissingRouteParamProperty = new(
        "NOF201",
        "Missing public property for route parameter",
        "Request type '{0}' does not contain a public property matching route parameter '{1}' (case-insensitive). Add a public property named '{1}'.",
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
        "Method '{0}' on service interface '{1}' must have exactly one request parameter (plus optional CancellationToken) and return Task or Task<T>",
        "GenerateService",
        DiagnosticSeverity.Error,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        RequestMustBeReferenceType,
        MissingRouteParamProperty,
        ClassMustHaveParameterlessCtor,
        InvalidServiceMethodSignature
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
        AnalyzeGenerateServiceAttribute(context, typeSymbol);
    }

    private static void AnalyzeLegacyHttpEndpointAttributes(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol)
    {
        var httpEndpointAttributes = typeSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString() == ExposeToHttpEndpointHelpers.HttpEndpointAttributeFqn)
            .ToList();

        if (httpEndpointAttributes.Count == 0)
        {
            return;
        }

        var typeLocation = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
        ValidateRequestPayloadShape(context, typeSymbol, typeLocation);

        var allProperties = ExposeToHttpEndpointHelpers.GetAllPublicProperties(typeSymbol);
        var propertyNames = new HashSet<string>(allProperties.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var attr in httpEndpointAttributes)
        {
            var attrLocation = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? typeLocation;
            var route = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1].Value as string : null;
            if (string.IsNullOrWhiteSpace(route))
            {
                continue;
            }

            ValidateRouteParameters(context, typeSymbol.Name, route!, propertyNames, attrLocation);
        }
    }

    private static void AnalyzeGenerateServiceAttribute(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol)
    {
        if (!ExposeToHttpEndpointHelpers.HasGenerateServiceAttribute(typeSymbol))
        {
            return;
        }

        var generateAttr = typeSymbol.GetAttributes()
            .First(a => a.AttributeClass?.ToDisplayString() == ExposeToHttpEndpointHelpers.GenerateServiceAttributeFqn);

        var attrLocation = generateAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation()
            ?? typeSymbol.Locations.FirstOrDefault()
            ?? Location.None;

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.MethodKind != MethodKind.Ordinary || method.IsImplicitlyDeclared)
            {
                continue;
            }

            var validRequestParameter = ExposeToHttpEndpointHelpers.TryGetRequestParameter(method, out var requestParameter);
            var validReturnType = ExposeToHttpEndpointHelpers.TryGetServiceReturnInfo(method, out _);
            if (!validRequestParameter || !validReturnType)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(InvalidServiceMethodSignature, method.Locations.FirstOrDefault() ?? attrLocation, method.Name, typeSymbol.Name));
                continue;
            }

            if (requestParameter!.Type is not INamedTypeSymbol requestType)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(InvalidServiceMethodSignature, method.Locations.FirstOrDefault() ?? attrLocation, method.Name, typeSymbol.Name));
                continue;
            }

            var methodHttpEndpointAttr = method.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ExposeToHttpEndpointHelpers.HttpEndpointAttributeFqn);
            if (methodHttpEndpointAttr is null)
            {
                continue;
            }

            var methodLocation = method.Locations.FirstOrDefault() ?? attrLocation;
            ValidateRequestPayloadShape(context, requestType, methodLocation);

            var route = methodHttpEndpointAttr.ConstructorArguments.Length > 1
                ? methodHttpEndpointAttr.ConstructorArguments[1].Value as string
                : null;
            if (string.IsNullOrWhiteSpace(route))
            {
                continue;
            }

            var allProperties = ExposeToHttpEndpointHelpers.GetAllPublicProperties(requestType);
            var propertyNames = new HashSet<string>(allProperties.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            ValidateRouteParameters(context, requestType.Name, route!, propertyNames, methodLocation);
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

    private static void ValidateRouteParameters(
        SymbolAnalysisContext context,
        string requestTypeName,
        string route,
        HashSet<string> propertyNames,
        Location location)
    {
        var routeParams = ExposeToHttpEndpointHelpers.ExtractRouteParameters(route);
        foreach (var routeParam in routeParams.Where(routeParam => !propertyNames.Contains(routeParam)))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(MissingRouteParamProperty, location, requestTypeName, routeParam));
        }
    }

}
