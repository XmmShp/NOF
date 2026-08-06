using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.Domain.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValueObjectOrderingAnalyzer : DiagnosticAnalyzer
{
    private const string ValueObjectInterfaceMetadataName = "NOF.Domain.IValueObject<T>";
    private const string ComparableInterfaceMetadataName = "System.IComparable";
    private const string GenericComparableInterfaceMetadataName = "System.IComparable<T>";

    public static readonly DiagnosticDescriptor OrderByShouldUsePrimitive = new(
        id: "NOF015",
        title: "Order value objects by their primitive value",
        messageFormat: "Value object '{0}' is used as a LINQ ordering key. Cast the selected value to its underlying type '{1}', or implement IComparable<{0}>.",
        category: "ValueObject",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [OrderByShouldUsePrimitive];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (!IsLinqOrderingMethod(method)
            || method.TypeArguments.Length < 2
            || !TryGetValueObject(method.TypeArguments[1], out var valueObjectType, out var primitiveType, out var nullableKey)
            || ImplementsComparison(valueObjectType))
        {
            return;
        }

        var keySelector = invocation.Arguments
            .FirstOrDefault(static argument => argument.Parameter?.Name == "keySelector");
        var location = keySelector?.Syntax.GetLocation() ?? invocation.Syntax.GetLocation();
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add("PrimitiveType", primitiveType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Add("NullableKey", nullableKey ? "true" : "false");
        context.ReportDiagnostic(Diagnostic.Create(
            OrderByShouldUsePrimitive,
            location,
            properties,
            valueObjectType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            primitiveType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static bool IsLinqOrderingMethod(IMethodSymbol method)
    {
        if (method.Name is not ("OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending"))
        {
            return false;
        }

        var containingType = (method.ReducedFrom ?? method).ContainingType;
        return containingType.ContainingNamespace.ToDisplayString() == "System.Linq"
            && containingType.Name is "Enumerable" or "Queryable";
    }

    private static bool TryGetValueObject(
        ITypeSymbol keyType,
        out INamedTypeSymbol valueObjectType,
        out ITypeSymbol primitiveType,
        out bool nullableKey)
    {
        nullableKey = false;
        if (keyType is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: 1
            } nullableType)
        {
            nullableKey = true;
            keyType = nullableType.TypeArguments[0];
        }

        if (keyType is not INamedTypeSymbol namedType)
        {
            valueObjectType = null!;
            primitiveType = null!;
            nullableKey = false;
            return false;
        }

        var valueObjectInterface = namedType.IsGenericType
            && namedType.OriginalDefinition.ToDisplayString() == ValueObjectInterfaceMetadataName
                ? namedType
                : namedType.AllInterfaces.FirstOrDefault(static @interface =>
                    @interface.IsGenericType
                    && @interface.OriginalDefinition.ToDisplayString() == ValueObjectInterfaceMetadataName);
        if (valueObjectInterface is null)
        {
            valueObjectType = null!;
            primitiveType = null!;
            nullableKey = false;
            return false;
        }

        valueObjectType = namedType;
        primitiveType = valueObjectInterface.TypeArguments[0];
        return true;
    }

    private static bool ImplementsComparison(INamedTypeSymbol valueObjectType)
    {
        foreach (var @interface in valueObjectType.AllInterfaces)
        {
            if (@interface.ToDisplayString() == ComparableInterfaceMetadataName)
            {
                return true;
            }

            if (@interface.IsGenericType
                && @interface.OriginalDefinition.ToDisplayString() == GenericComparableInterfaceMetadataName
                && SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], valueObjectType))
            {
                return true;
            }
        }

        return false;
    }
}
