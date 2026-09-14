using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.Application.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SelectMapProjectionAnalyzer : DiagnosticAnalyzer
{
    private const string MapperMetadataName = "NOF.Application.IMapper";
    private const string QueryableMetadataName = "System.Linq.Queryable";

    public static readonly DiagnosticDescriptor UseProjectTo = new(
        id: "NOF026",
        title: "Use ProjectTo for query mapping",
        messageFormat: "Use ProjectTo<{0}>(mapper) instead of Select(x => mapper.Map<{1}, {0}>(x)) to keep the mapping in the query projection",
        category: "NOF.Application.Mapping",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [UseProjectTo];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var mapperType = compilationContext.Compilation.GetTypeByMetadataName(MapperMetadataName);
            var queryableType = compilationContext.Compilation.GetTypeByMetadataName(QueryableMetadataName);
            if (mapperType is null || queryableType is null)
            {
                return;
            }

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, mapperType, queryableType),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol mapperType,
        INamedTypeSymbol queryableType)
    {
        var selectInvocation = (IInvocationOperation)context.Operation;
        if (!IsQueryableSelect(selectInvocation.TargetMethod, queryableType)
            || selectInvocation.Arguments.Length != 2)
        {
            return;
        }

        var selector = Unwrap(selectInvocation.Arguments[1].Value) as IAnonymousFunctionOperation;
        if (selector is null
            || selector.Symbol.Parameters.Length != 1
            || GetReturnedValue(selector) is not { } returnedValue)
        {
            return;
        }

        returnedValue = Unwrap(returnedValue);
        if (returnedValue is not IInvocationOperation mapInvocation
            || !IsMapperMap(mapInvocation.TargetMethod, mapperType)
            || mapInvocation.TargetMethod.TypeArguments.Length != 2
            || mapInvocation.Arguments.Length == 0
            || mapInvocation.Arguments.Skip(1).Any(static argument => !argument.IsImplicit)
            || !IsParameterReference(mapInvocation.Arguments[0].Value, selector.Symbol.Parameters[0]))
        {
            return;
        }

        var sourceType = mapInvocation.TargetMethod.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var destinationType = mapInvocation.TargetMethod.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(
            UseProjectTo,
            mapInvocation.Syntax.GetLocation(),
            destinationType,
            sourceType));
    }

    private static bool IsQueryableSelect(IMethodSymbol method, INamedTypeSymbol queryableType)
        => method.Name == nameof(Queryable.Select)
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, queryableType);

    private static bool IsMapperMap(IMethodSymbol method, INamedTypeSymbol mapperType)
    {
        if (method.Name != "Map" || !method.IsGenericMethod)
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(method.ContainingType, mapperType)
            || method.ContainingType.AllInterfaces.Any(@interface =>
                SymbolEqualityComparer.Default.Equals(@interface, mapperType));
    }

    private static IOperation? GetReturnedValue(IAnonymousFunctionOperation selector)
    {
        var returns = selector.Body.Operations.OfType<IReturnOperation>().ToArray();
        return returns.Length == 1 ? returns[0].ReturnedValue : null;
    }

    private static bool IsParameterReference(IOperation operation, IParameterSymbol parameter)
        => Unwrap(operation) is IParameterReferenceOperation parameterReference
            && SymbolEqualityComparer.Default.Equals(parameterReference.Parameter, parameter);

    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }
}
