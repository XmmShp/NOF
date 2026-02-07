using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace NOF.Contract.SourceGenerator;

/// <summary>
/// Diagnostic analyzer for ExposeToHttpEndpoint usage validation.
/// Validates that request types are reference types (class/record), have matching public properties
/// for all route parameters, and that classes have a parameterless constructor.
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
        "ExposeToHttpEndpoint",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// Route parameter has no matching public property on the request type.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingRouteParamProperty = new(
        "NOF201",
        "Missing public property for route parameter",
        "Request type '{0}' does not contain a public property matching route parameter '{1}' (case-insensitive). Add a public property named '{1}'.",
        "ExposeToHttpEndpoint",
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
        "ExposeToHttpEndpoint",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// OperationName must be a valid C# identifier.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidOperationName = new(
        "NOF203",
        "OperationName must be a valid C# identifier",
        "OperationName '{0}' on request type '{1}' is not a valid C# identifier. It must start with a letter or underscore and contain only letters, digits, or underscores.",
        "ExposeToHttpEndpoint",
        DiagnosticSeverity.Error,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        RequestMustBeReferenceType,
        MissingRouteParamProperty,
        ClassMustHaveParameterlessCtor,
        InvalidOperationName
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

        // Only analyze types with ExposeToHttpEndpointAttribute
        var endpointAttributes = typeSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString() == "NOF.ExposeToHttpEndpointAttribute")
            .ToList();

        if (endpointAttributes.Count == 0)
            return;

        // Only analyze types that implement IRequest or IRequest<T>
        if (!IsRequestType(typeSymbol))
            return;

        var typeLocation = typeSymbol.Locations.FirstOrDefault() ?? Location.None;

        // Rule 1: Must be a reference type (class or record), not struct
        if (typeSymbol.IsValueType)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(RequestMustBeReferenceType, typeLocation, typeSymbol.Name));
            return; // No point checking further
        }

        // Determine if this is a record
        var isRecord = typeSymbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(t => t is RecordDeclarationSyntax);

        // Rule 2: If it's a class (not record), must have a public parameterless constructor
        if (!isRecord)
        {
            var hasParameterlessCtor = typeSymbol.Constructors
                .Any(c => c.DeclaredAccessibility == Accessibility.Public
                          && !c.IsStatic
                          && c.Parameters.Length == 0);

            // If there are no explicitly declared instance constructors, the compiler generates a default one
            var hasExplicitCtors = typeSymbol.Constructors
                .Any(c => !c.IsStatic && !c.IsImplicitlyDeclared);

            if (hasExplicitCtors && !hasParameterlessCtor)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(ClassMustHaveParameterlessCtor, typeLocation, typeSymbol.Name));
            }
        }

        // Rule 3: For each attribute, check that all route parameters have matching public properties
        var allProperties = GetAllPublicProperties(typeSymbol);
        var propertyNames = new System.Collections.Generic.HashSet<string>(
            allProperties.Select(p => p.Name),
            System.StringComparer.OrdinalIgnoreCase);

        foreach (var attr in endpointAttributes)
        {
            var attrLocation = attr.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ?? typeLocation;

            // Rule 4: OperationName must be a valid C# identifier
            var operationName = attr.NamedArguments
                .FirstOrDefault(arg => arg.Key == "OperationName").Value.Value as string;
            if (operationName != null && !SyntaxFacts.IsValidIdentifier(operationName))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(InvalidOperationName, attrLocation, operationName, typeSymbol.Name));
            }

            // Rule 3: Check route parameters have matching public properties
            var route = attr.ConstructorArguments.Length > 1
                ? attr.ConstructorArguments[1].Value as string
                : null;

            if (string.IsNullOrEmpty(route))
                continue;

            var routeParams = ExtractRouteParameters(route!);

            foreach (var routeParam in routeParams)
            {
                if (!propertyNames.Contains(routeParam))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(MissingRouteParamProperty, attrLocation, typeSymbol.Name, routeParam));
                }
            }
        }
    }

    private static bool IsRequestType(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(i =>
            i.ToDisplayString() == "NOF.IRequest"
            || (i is { IsGenericType: true }
                && i.OriginalDefinition.ToDisplayString() == "NOF.IRequest<TResponse>"));
    }

    private static System.Collections.Generic.List<IPropertySymbol> GetAllPublicProperties(INamedTypeSymbol typeSymbol)
    {
        var properties = new System.Collections.Generic.List<IPropertySymbol>();
        var seen = new System.Collections.Generic.HashSet<string>();
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

    private static System.Collections.Generic.List<string> ExtractRouteParameters(string route)
    {
        var result = new System.Collections.Generic.List<string>();
        var matches = Regex.Matches(route, @"\{(\w+)\}");
        foreach (Match match in matches)
        {
            result.Add(match.Groups[1].Value);
        }
        return result;
    }
}
