using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace NOF.Application.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProjectToOrderingAnalyzer : DiagnosticAnalyzer
{
    private const string QueryableMetadataName = "System.Linq.IQueryable";
    private const string MappingExtensionsMetadataName = "System.Linq.MappingQueryableExtensions";

    private static readonly ImmutableHashSet<string> _transparentQueryMethods =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "AsQueryable",
            "AsAsyncQueryable",
            "TagWith",
            "TagWithCallSite");

    private static readonly ImmutableHashSet<string> _terminalMethodsWithQueryLogic =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "All",
            "AllAsync",
            "Any",
            "AnyAsync",
            "Average",
            "AverageAsync",
            "Count",
            "CountAsync",
            "First",
            "FirstAsync",
            "FirstOrDefault",
            "FirstOrDefaultAsync",
            "Last",
            "LastAsync",
            "LastOrDefault",
            "LastOrDefaultAsync",
            "LongCount",
            "LongCountAsync",
            "Max",
            "MaxAsync",
            "Min",
            "MinAsync",
            "Single",
            "SingleAsync",
            "SingleOrDefault",
            "SingleOrDefaultAsync",
            "Sum",
            "SumAsync");

    public static readonly DiagnosticDescriptor ProjectionShouldBeLast = new(
        id: "NOF025",
        title: "Keep ProjectTo as the final query-shaping operation",
        messageFormat: "Query operation '{0}' is applied after ProjectTo. Apply filtering, ordering, paging, and shape-changing operations before projection when possible.",
        category: "NOF.Application.Mapping",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [ProjectionShouldBeLast];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var queryableType = compilationContext.Compilation.GetTypeByMetadataName(QueryableMetadataName);
            var mappingExtensionsType = compilationContext.Compilation.GetTypeByMetadataName(MappingExtensionsMetadataName);
            if (queryableType is null || mappingExtensionsType is null)
            {
                return;
            }

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(
                    operationContext,
                    queryableType,
                    mappingExtensionsType),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol queryableType,
        INamedTypeSymbol mappingExtensionsType)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!IsPostProjectionOperation(invocation, queryableType)
            || IsTransparentQueryMethod(invocation.TargetMethod))
        {
            return;
        }

        foreach (var source in GetQuerySources(invocation, queryableType))
        {
            var visitedLocals = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
            if (!OriginatesFromProjectTo(
                    source,
                    context,
                    queryableType,
                    mappingExtensionsType,
                    visitedLocals))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                ProjectionShouldBeLast,
                GetMethodLocation(invocation),
                invocation.TargetMethod.Name));
            return;
        }
    }

    private static bool IsPostProjectionOperation(
        IInvocationOperation invocation,
        INamedTypeSymbol queryableType)
    {
        if (IsQueryable(invocation.Type, queryableType))
        {
            return true;
        }

        var methodName = invocation.TargetMethod.Name;
        if (methodName is "ExecuteDelete" or "ExecuteDeleteAsync" or "ExecuteUpdate" or "ExecuteUpdateAsync")
        {
            return true;
        }

        return _terminalMethodsWithQueryLogic.Contains(methodName)
            && invocation.Arguments.Any(static argument =>
                argument.Parameter?.Name is "predicate" or "selector");
    }

    private static bool OriginatesFromProjectTo(
        IOperation operation,
        OperationAnalysisContext context,
        INamedTypeSymbol queryableType,
        INamedTypeSymbol mappingExtensionsType,
        HashSet<ILocalSymbol> visitedLocals)
    {
        operation = Unwrap(operation);

        if (operation is IInvocationOperation invocation)
        {
            if (IsProjectTo(invocation.TargetMethod, mappingExtensionsType))
            {
                return true;
            }

            if (!IsTransparentQueryMethod(invocation.TargetMethod))
            {
                return false;
            }

            return GetQuerySources(invocation, queryableType).Any(source =>
                OriginatesFromProjectTo(
                    source,
                    context,
                    queryableType,
                    mappingExtensionsType,
                    visitedLocals));
        }

        if (operation is not ILocalReferenceOperation localReference
            || !visitedLocals.Add(localReference.Local)
            || HasPriorWrite(localReference, context))
        {
            return false;
        }

        var declaration = localReference.Local.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(context.CancellationToken))
            .OfType<VariableDeclaratorSyntax>()
            .SingleOrDefault();
        if (declaration?.Initializer?.Value is not { } initializer
            || initializer.SpanStart >= localReference.Syntax.SpanStart)
        {
            return false;
        }

        var semanticModel = context.Operation.SemanticModel;
        if (semanticModel is null)
        {
            return false;
        }

        var initializerOperation = semanticModel.GetOperation(initializer, context.CancellationToken);
        return initializerOperation is not null
            && OriginatesFromProjectTo(
                initializerOperation,
                context,
                queryableType,
                mappingExtensionsType,
                visitedLocals);
    }

    private static IEnumerable<IOperation> GetQuerySources(
        IInvocationOperation invocation,
        INamedTypeSymbol queryableType)
    {
        if (invocation.Instance is { } instance
            && IsQueryable(instance.Type, queryableType))
        {
            yield return instance;
        }

        foreach (var argument in invocation.Arguments)
        {
            var value = Unwrap(argument.Value);
            if (IsQueryable(argument.Parameter?.Type, queryableType)
                || IsQueryable(argument.Value.Type, queryableType)
                || IsQueryable(value.Type, queryableType))
            {
                yield return value;
            }
        }
    }

    private static bool HasPriorWrite(
        ILocalReferenceOperation localReference,
        OperationAnalysisContext context)
    {
        var declaration = localReference.Local.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(context.CancellationToken))
            .OfType<VariableDeclaratorSyntax>()
            .SingleOrDefault();
        if (declaration is null)
        {
            return true;
        }

        var scope = declaration.Ancestors().FirstOrDefault(static node =>
                node is AnonymousFunctionExpressionSyntax
                    or LocalFunctionStatementSyntax
                    or BaseMethodDeclarationSyntax
                    or AccessorDeclarationSyntax)
            ?? declaration.SyntaxTree.GetRoot(context.CancellationToken);
        var semanticModel = context.Operation.SemanticModel;
        if (semanticModel is null)
        {
            return true;
        }

        var useStart = localReference.Syntax.SpanStart;

        foreach (var assignment in scope.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Span.End > useStart
                || !ReferencesLocal(assignment.Left, localReference.Local, semanticModel, context.CancellationToken))
            {
                continue;
            }

            return true;
        }

        foreach (var unary in scope.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>())
        {
            if (unary.Span.End <= useStart
                && ReferencesLocal(unary.Operand, localReference.Local, semanticModel, context.CancellationToken))
            {
                return true;
            }
        }

        foreach (var unary in scope.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>())
        {
            if (unary.Span.End <= useStart
                && ReferencesLocal(unary.Operand, localReference.Local, semanticModel, context.CancellationToken))
            {
                return true;
            }
        }

        foreach (var argument in scope.DescendantNodes().OfType<ArgumentSyntax>())
        {
            if (argument.Span.End <= useStart
                && argument.RefKindKeyword.Kind() is Microsoft.CodeAnalysis.CSharp.SyntaxKind.RefKeyword
                    or Microsoft.CodeAnalysis.CSharp.SyntaxKind.OutKeyword
                && ReferencesLocal(argument.Expression, localReference.Local, semanticModel, context.CancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReferencesLocal(
        SyntaxNode node,
        ILocalSymbol local,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
        => node.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                local));

    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private static bool IsProjectTo(
        IMethodSymbol method,
        INamedTypeSymbol mappingExtensionsType)
    {
        method = method.ReducedFrom ?? method;
        return method.Name == "ProjectTo"
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, mappingExtensionsType);
    }

    private static bool IsTransparentQueryMethod(IMethodSymbol method)
        => _transparentQueryMethods.Contains(method.Name);

    private static bool IsQueryable(ITypeSymbol? type, INamedTypeSymbol queryableType)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(namedType, queryableType)
            || namedType.AllInterfaces.Any(@interface =>
                SymbolEqualityComparer.Default.Equals(@interface, queryableType));
    }

    private static Location GetMethodLocation(IInvocationOperation invocation)
        => invocation.Syntax is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax memberAccess
        }
            ? memberAccess.Name.GetLocation()
            : invocation.Syntax.GetLocation();
}
