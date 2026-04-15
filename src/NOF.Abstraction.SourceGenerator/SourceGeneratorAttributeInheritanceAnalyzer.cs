using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.Abstraction.SourceGenerator;

/// <summary>
/// Ensures attributes intended for source-generator consumption are sealed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SourceGeneratorAttributeInheritanceAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeForSourceGeneratorFullName = "NOF.Annotation.AttributeForSourceGenerator";

    public static readonly DiagnosticDescriptor AttributeForSourceGeneratorMustBeSealed = new(
        id: "NOF400",
        title: "Source-generator attribute must be sealed",
        messageFormat: "Attribute '{0}' inherits AttributeForSourceGenerator and must be sealed because source generators usually do not account for attribute inheritance",
        category: "SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        AttributeForSourceGeneratorMustBeSealed
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
        {
            var markerBaseType = startContext.Compilation.GetTypeByMetadataName(AttributeForSourceGeneratorFullName);
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

        if (!DerivesFrom(namedType, markerBaseType))
        {
            return;
        }

        if (namedType.IsSealed)
        {
            return;
        }

        var location = namedType.Locations.FirstOrDefault() ?? Location.None;
        context.ReportDiagnostic(Diagnostic.Create(
            AttributeForSourceGeneratorMustBeSealed,
            location,
            namedType.Name));
    }

    private static bool DerivesFrom(INamedTypeSymbol namedType, INamedTypeSymbol baseType)
    {
        var current = namedType.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
