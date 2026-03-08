using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.Contract.SourceGenerator;

/// <summary>
/// Diagnostic analyzer for HttpEndpoint / PublicApi / GenerateService attribute validation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ExposeToHttpEndpointAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Request type must be a reference type (class or record), not a struct.
    /// </summary>
    public static readonly DiagnosticDescriptor RequestMustBeReferenceType = new(
        "NOF200",
        "Request must be a reference type",
        "Request type '{0}' must be a class or record, not a struct",
        "PublicApi",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// Route parameter has no matching public property on the request type.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingRouteParamProperty = new(
        "NOF201",
        "Missing public property for route parameter",
        "Request type '{0}' does not contain a public property matching route parameter '{1}' (case-insensitive). Add a public property named '{1}'.",
        "HttpEndpoint",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// Class request type must have a parameterless constructor (or no explicit constructors).
    /// Records are exempt because they always have a copy constructor and can use primary ctors.
    /// </summary>
    public static readonly DiagnosticDescriptor ClassMustHaveParameterlessCtor = new(
        "NOF202",
        "Class request must have a parameterless constructor",
        "Request class '{0}' must have a public parameterless constructor. Records may use primary constructors, but classes must have a parameterless constructor.",
        "HttpEndpoint",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// OperationName must be a valid C# identifier.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidOperationName = new(
        "NOF203",
        "OperationName must be a valid C# identifier",
        "OperationName '{0}' on request type '{1}' is not a valid C# identifier. It must start with a letter or underscore and contain only letters, digits, or underscores.",
        "PublicApi",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// [HttpEndpoint] requires [PublicApi] to also be present on the type.
    /// </summary>
    public static readonly DiagnosticDescriptor HttpEndpointRequiresPublicApi = new(
        "NOF204",
        "[HttpEndpoint] requires [PublicApi]",
        "Request type '{0}' has [HttpEndpoint] but is missing [PublicApi]. Add [PublicApi] to the type.",
        "HttpEndpoint",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// ExtraTypes in [GenerateService] must implement IRequest or IRequest&lt;T&gt;.
    /// </summary>
    public static readonly DiagnosticDescriptor ExtraTypeMustBeRequest = new(
        "NOF205",
        "ExtraType must implement IRequest",
        "Type '{0}' specified in ExtraTypes does not implement IRequest or IRequest<T>",
        "GenerateService",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// ExtraTypes in [GenerateService] must have [PublicApi].
    /// </summary>
    public static readonly DiagnosticDescriptor ExtraTypeMustHavePublicApi = new(
        "NOF206",
        "ExtraType must have [PublicApi]",
        "Type '{0}' specified in ExtraTypes does not have [PublicApi] attribute",
        "GenerateService",
        DiagnosticSeverity.Error,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        RequestMustBeReferenceType,
        MissingRouteParamProperty,
        ClassMustHaveParameterlessCtor,
        InvalidOperationName,
        HttpEndpointRequiresPublicApi,
        ExtraTypeMustBeRequest,
        ExtraTypeMustHavePublicApi
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

        // Analyze [HttpEndpoint] on request types
        AnalyzeHttpEndpointAttributes(context, typeSymbol);

        // Analyze [PublicApi] on request types
        AnalyzePublicApiAttribute(context, typeSymbol);

        // Analyze [GenerateService] on interfaces
        AnalyzeGenerateServiceAttribute(context, typeSymbol);
    }

    private static void AnalyzeHttpEndpointAttributes(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol)
    {
        var httpEndpointAttributes = typeSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString() == ExposeToHttpEndpointHelpers.HttpEndpointAttributeFqn)
            .ToList();

        if (httpEndpointAttributes.Count == 0)
        {
            return;
        }

        var typeLocation = typeSymbol.Locations.FirstOrDefault() ?? Location.None;

        // Rule: [HttpEndpoint] requires [PublicApi]
        if (!ExposeToHttpEndpointHelpers.HasPublicApiAttribute(typeSymbol))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(HttpEndpointRequiresPublicApi, typeLocation, typeSymbol.Name));
            return;
        }

        // Only validate further if it implements IRequest
        if (!ExposeToHttpEndpointHelpers.IsRequestType(typeSymbol))
        {
            return;
        }

        // Rule: Must be a reference type
        if (typeSymbol.IsValueType)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(RequestMustBeReferenceType, typeLocation, typeSymbol.Name));
            return;
        }

        // Determine if this is a record
        var isRecord = typeSymbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(t => t is RecordDeclarationSyntax);

        // Rule: class (not record) must have parameterless ctor
        if (!isRecord)
        {
            var hasParameterlessCtor = typeSymbol.Constructors
                .Any(c => c.DeclaredAccessibility == Accessibility.Public
                          && c is { IsStatic: false, Parameters.Length: 0 });

            var hasExplicitCtors = typeSymbol.Constructors
                .Any(c => !c.IsStatic && !c.IsImplicitlyDeclared);

            if (hasExplicitCtors && !hasParameterlessCtor)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(ClassMustHaveParameterlessCtor, typeLocation, typeSymbol.Name));
            }
        }

        // Rule: route parameters must have matching properties
        var allProperties = ExposeToHttpEndpointHelpers.GetAllPublicProperties(typeSymbol);
        var propertyNames = new HashSet<string>(
            allProperties.Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var attr in httpEndpointAttributes)
        {
            var attrLocation = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? typeLocation;

            var route = attr.ConstructorArguments.Length > 1
                ? attr.ConstructorArguments[1].Value as string
                : null;

            if (string.IsNullOrEmpty(route))
            {
                continue;
            }

            var routeParams = ExposeToHttpEndpointHelpers.ExtractRouteParameters(route!);

            foreach (var routeParam in routeParams.Where(routeParam => !propertyNames.Contains(routeParam)))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(MissingRouteParamProperty, attrLocation, typeSymbol.Name, routeParam));
            }
        }
    }

    private static void AnalyzePublicApiAttribute(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol)
    {
        var publicApiAttr = typeSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == ExposeToHttpEndpointHelpers.PublicApiAttributeFqn);

        if (publicApiAttr is null)
        {
            return;
        }

        var typeLocation = typeSymbol.Locations.FirstOrDefault() ?? Location.None;

        // Validate OperationName if specified
        var operationName = publicApiAttr.NamedArguments
            .FirstOrDefault(arg => arg.Key == "OperationName").Value.Value as string;

        if (operationName is not null && !SyntaxFacts.IsValidIdentifier(operationName))
        {
            var attrLocation = publicApiAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? typeLocation;
            context.ReportDiagnostic(
                Diagnostic.Create(InvalidOperationName, attrLocation, operationName, typeSymbol.Name));
        }

        // Validate: must be a reference type if it's a request
        if (ExposeToHttpEndpointHelpers.IsRequestType(typeSymbol) && typeSymbol.IsValueType)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(RequestMustBeReferenceType, typeLocation, typeSymbol.Name));
        }
    }

    private static void AnalyzeGenerateServiceAttribute(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol)
    {
        var gsAttr = typeSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == ExposeToHttpEndpointHelpers.GenerateServiceAttributeFqn);

        if (gsAttr is null)
        {
            return;
        }

        var attrLocation = gsAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                           ?? typeSymbol.Locations.FirstOrDefault()
                           ?? Location.None;

        // Validate ExtraTypes
        var extraTypesArg = gsAttr.NamedArguments
            .FirstOrDefault(a => a.Key == "ExtraTypes").Value;

        if (extraTypesArg.Kind != TypedConstantKind.Array || extraTypesArg.Values.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var et in extraTypesArg.Values)
        {
            if (et.Value is not INamedTypeSymbol extraType)
            {
                continue;
            }

            if (!ExposeToHttpEndpointHelpers.IsRequestType(extraType))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(ExtraTypeMustBeRequest, attrLocation, extraType.Name));
            }

            if (!ExposeToHttpEndpointHelpers.HasPublicApiAttribute(extraType))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(ExtraTypeMustHavePublicApi, attrLocation, extraType.Name));
            }
        }
    }
}
