using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.Domain.SourceGenerator;

/// <summary>
/// Rejects source-level value object construction paths that bypass the generated factories.
/// Runtime guards in the generated value object cover implicit zero-initialization paths that
/// cannot be diagnosed reliably, such as array elements and generic <c>default(T)</c> values.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValueObjectInitializationAnalyzer : DiagnosticAnalyzer
{
    private const string ValueObjectInterfaceMetadataName = "NOF.Domain.IValueObject`1";

    public static readonly DiagnosticDescriptor UseFactory = new(
        id: "NOF018",
        title: "Create value objects through their factory",
        messageFormat: "Value object '{0}' cannot be initialized with {1}; use '{0}.Of(...)' or another generated factory",
        category: "ValueObject",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Struct default initialization bypasses value object normalization and validation.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [UseFactory];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static startContext =>
        {
            var valueObjectInterface = startContext.Compilation.GetTypeByMetadataName(
                ValueObjectInterfaceMetadataName);
            if (valueObjectInterface is null)
            {
                return;
            }

            startContext.RegisterOperationAction(
                operationContext => AnalyzeDefaultValue(operationContext, valueObjectInterface),
                OperationKind.DefaultValue);
            startContext.RegisterOperationAction(
                operationContext => AnalyzeObjectCreation(operationContext, valueObjectInterface),
                OperationKind.ObjectCreation);
        });
    }

    private static void AnalyzeDefaultValue(
        OperationAnalysisContext context,
        INamedTypeSymbol valueObjectInterface)
    {
        var operation = (IDefaultValueOperation)context.Operation;
        if (IsValueObject(operation.Type, valueObjectInterface))
        {
            Report(context, operation.Type!, operation.Syntax.GetLocation(), "default");
        }
    }

    private static void AnalyzeObjectCreation(
        OperationAnalysisContext context,
        INamedTypeSymbol valueObjectInterface)
    {
        var operation = (IObjectCreationOperation)context.Operation;
        if (operation.Arguments.IsEmpty && IsValueObject(operation.Type, valueObjectInterface))
        {
            Report(context, operation.Type!, operation.Syntax.GetLocation(), "new()");
        }
    }

    private static bool IsValueObject(ITypeSymbol? type, INamedTypeSymbol valueObjectInterface)
        => type is INamedTypeSymbol { TypeKind: TypeKind.Struct } namedType
            && namedType.AllInterfaces.Any(@interface =>
                SymbolEqualityComparer.Default.Equals(
                    @interface.OriginalDefinition,
                    valueObjectInterface));

    private static void Report(
        OperationAnalysisContext context,
        ITypeSymbol type,
        Location location,
        string initialization)
        => context.ReportDiagnostic(Diagnostic.Create(
            UseFactory,
            location,
            type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            initialization));
}
