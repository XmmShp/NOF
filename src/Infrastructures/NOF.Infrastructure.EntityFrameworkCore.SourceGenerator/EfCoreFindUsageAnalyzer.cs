using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Immutable;

namespace NOF.Infrastructure.EntityFrameworkCore.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EfCoreFindUsageAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _dbSetFindAsyncDescriptor = new(
        id: "NOF302",
        title: "Avoid calling FindAsync on EF Core persistence types",
        messageFormat: "Calling FindAsync(...) can bypass provider-level query rules such as filters. Prefer FirstOrDefaultAsync(...) instead. If you need an in-memory tracked instance, publish that instance as an event payload via PublishAsEvent(...).",
        category: "NOF.Infrastructure.EntityFrameworkCore",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _dbSetFindDescriptor = new(
        id: "NOF303",
        title: "Avoid calling Find on EF Core persistence types",
        messageFormat: "Calling Find(...) can bypass provider-level query rules such as filters. Prefer FirstOrDefault(...) instead. If you need an in-memory tracked instance, publish that instance as an event payload via PublishAsEvent(...).",
        category: "NOF.Infrastructure.EntityFrameworkCore",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _dbContextInheritanceDescriptor = new(
        id: "NOF304",
        title: "Prefer inheriting NOFDbContext",
        messageFormat: "DbContext implementations should prefer inheriting NOFDbContext so tenant, soft-delete, and NOF persistence conventions remain active",
        category: "NOF.Infrastructure.EntityFrameworkCore",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [_dbSetFindAsyncDescriptor, _dbSetFindDescriptor, _dbContextInheritanceDescriptor];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
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

        if (IsWithinNofDbContextImplementation(context.ContainingSymbol))
        {
            return;
        }

        if (methodSymbol.Name == "FindAsync" && IsFindAsyncOnPersistenceType(methodSymbol.ContainingType))
        {
            context.ReportDiagnostic(Diagnostic.Create(_dbSetFindAsyncDescriptor, invocation.Syntax.GetLocation()));
            return;
        }

        if (methodSymbol.Name == "Find" && IsFindOnPersistenceType(methodSymbol.ContainingType))
        {
            context.ReportDiagnostic(Diagnostic.Create(_dbSetFindDescriptor, invocation.Syntax.GetLocation()));
        }
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol namedType || namedType.TypeKind != TypeKind.Class)
        {
            return;
        }

        var baseType = namedType.BaseType;
        if (baseType is null)
        {
            return;
        }

        if (ImplementsOrInherits(namedType, IsNofDbContextType))
        {
            return;
        }

        if (ImplementsOrInherits(namedType, IsEfCoreDbContextType))
        {
            context.ReportDiagnostic(Diagnostic.Create(_dbContextInheritanceDescriptor, namedType.Locations[0]));
        }
    }

    private static bool IsFindAsyncOnPersistenceType(INamedTypeSymbol? containingType)
        => ImplementsOrInherits(containingType, IsEfCoreDbContextType)
           || ImplementsOrInherits(containingType, IsEfCoreDbSetType);

    private static bool IsFindOnPersistenceType(INamedTypeSymbol? containingType)
        => IsFindAsyncOnPersistenceType(containingType);

    private static bool ImplementsOrInherits(INamedTypeSymbol? symbol, Func<INamedTypeSymbol, bool> predicate)
    {
        for (var current = symbol; current is not null; current = current.BaseType)
        {
            if (predicate(current.OriginalDefinition))
            {
                return true;
            }
        }

        if (symbol is not null)
        {
            foreach (var @interface in symbol.AllInterfaces)
            {
                if (predicate(@interface.OriginalDefinition))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsEfCoreDbContextType(INamedTypeSymbol type)
        => type.Name == "DbContext"
           && type.Arity == 0
           && type.ContainingNamespace.ToDisplayString() == "Microsoft.EntityFrameworkCore";

    private static bool IsNofDbContextType(INamedTypeSymbol type)
        => type.Name == "NOFDbContext"
           && type.Arity == 0
           && type.ContainingNamespace.ToDisplayString() == "NOF.Infrastructure.EntityFrameworkCore";

    private static bool IsWithinNofDbContextImplementation(ISymbol? symbol)
        => ImplementsOrInherits(symbol?.ContainingType, IsNofDbContextType);

    private static bool IsEfCoreDbSetType(INamedTypeSymbol type)
        => type.Name == "DbSet"
           && type.Arity == 1
           && type.ContainingNamespace.ToDisplayString() == "Microsoft.EntityFrameworkCore";
}
