using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.Abstraction.SourceGenerator;

/// <summary>
/// Enforces that classes deriving from ClassForSourceGenerator are not inherited more than once.
/// Allowed:
/// - A : ClassForSourceGenerator
/// - B : A
/// Disallowed:
/// - C : B
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClassForSourceGeneratorInheritanceAnalyzer : DiagnosticAnalyzer
{
    private const string MarkerBaseTypeFullName = "NOF.Abstraction.ClassForSourceGenerator";

    public static readonly DiagnosticDescriptor TooDeepInheritanceRule = new(
        id: "NOF401",
        title: "ClassForSourceGenerator inheritance too deep",
        messageFormat: "Type '{0}' inherits from ClassForSourceGenerator more than one level. Inherit at most once from a ClassForSourceGenerator-derived type.",
        category: "SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [TooDeepInheritanceRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
        {
            var markerBaseType = startContext.Compilation.GetTypeByMetadataName(MarkerBaseTypeFullName);
            if (markerBaseType is null)
            {
                return;
            }

            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, markerBaseType),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, INamedTypeSymbol markerBaseType)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;
        if (namedType.TypeKind != TypeKind.Class)
        {
            return;
        }

        if (SymbolEqualityComparer.Default.Equals(namedType, markerBaseType))
        {
            return;
        }

        var distance = GetDistanceToMarker(namedType, markerBaseType);
        // distance: 1 -> direct; 2 -> one extra level; 3+ -> not allowed
        if (distance <= 2)
        {
            return;
        }

        var location = namedType.Locations.FirstOrDefault() ?? Location.None;
        context.ReportDiagnostic(Diagnostic.Create(
            TooDeepInheritanceRule,
            location,
            namedType.Name));
    }

    private static int GetDistanceToMarker(INamedTypeSymbol namedType, INamedTypeSymbol markerBaseType)
    {
        var distance = 0;
        var current = namedType.BaseType;
        while (current is not null)
        {
            distance++;
            if (SymbolEqualityComparer.Default.Equals(current, markerBaseType))
            {
                return distance;
            }
            current = current.BaseType;
        }
        return 0;
    }
}
