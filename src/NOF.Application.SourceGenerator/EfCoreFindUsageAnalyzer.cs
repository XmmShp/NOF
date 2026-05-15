using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Immutable;

namespace NOF.Application.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EfCoreFindUsageAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _dbSetFindAsyncDescriptor = new(
        id: "NOF302",
        title: "Avoid calling FindAsync directly",
        messageFormat: "Calling FindAsync(...) bypasses EF Core query filters. Prefer FirstOrDefaultAsync(...) instead. If you need an in-memory tracked instance, publish that instance as an event payload via PublishAsEvent(...).",
        category: "NOF.Application",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _dbSetFindDescriptor = new(
        id: "NOF303",
        title: "Avoid calling Find directly",
        messageFormat: "Calling Find(...) bypasses EF Core query filters. Prefer FirstOrDefault(...) instead. If you need an in-memory tracked instance, publish that instance as an event payload via PublishAsEvent(...).",
        category: "NOF.Application",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [_dbSetFindAsyncDescriptor, _dbSetFindDescriptor];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        if (context.Operation is not IInvocationOperation invocation)
        {
            return;
        }

        var methodSymbol = invocation.TargetMethod;

        if (methodSymbol is null)
        {
            return;
        }

        if (methodSymbol.Name == "FindAsync" && IsFindAsyncOnEfCoreType(methodSymbol.ContainingType))
        {
            context.ReportDiagnostic(Diagnostic.Create(_dbSetFindAsyncDescriptor, invocation.Syntax.GetLocation()));
            return;
        }

        if (methodSymbol.Name == "Find" && IsFindOnEfCoreType(methodSymbol.ContainingType))
        {
            context.ReportDiagnostic(Diagnostic.Create(_dbSetFindDescriptor, invocation.Syntax.GetLocation()));
        }
    }

    private static bool IsFindAsyncOnEfCoreType(INamedTypeSymbol? containingType)
        => InheritsFromOrEquals(containingType, IsDbContextType)
           || InheritsFromOrEquals(containingType, IsDbSetType);

    private static bool IsFindOnEfCoreType(INamedTypeSymbol? containingType)
        => InheritsFromOrEquals(containingType, IsDbContextType)
           || InheritsFromOrEquals(containingType, IsDbSetType);

    private static bool InheritsFromOrEquals(INamedTypeSymbol? symbol, Func<INamedTypeSymbol, bool> predicate)
    {
        for (var current = symbol; current is not null; current = current.BaseType)
        {
            if (predicate(current.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDbContextType(INamedTypeSymbol type)
        => type.Name == "DbContext"
           && type.Arity == 0
           && type.ContainingNamespace.ToDisplayString() == "Microsoft.EntityFrameworkCore";

    private static bool IsDbSetType(INamedTypeSymbol type)
        => type.Name == "DbSet"
           && type.Arity == 1
           && type.ContainingNamespace.ToDisplayString() == "Microsoft.EntityFrameworkCore";
}
