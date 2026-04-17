using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

namespace NOF.Application.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DbSetFindUsageAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _dbSetFindAsyncDescriptor = new(
        id: "NOF302",
        title: "Do not call DbSet.FindAsync directly",
        messageFormat: "Avoid calling DbSet<TEntity>.FindAsync(...). Use DbContext.FindAsync<TEntity>(...) so NOF can apply tenant key rules consistently.",
        category: "NOF.Application",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _dbSetFindDescriptor = new(
        id: "NOF303",
        title: "Do not call DbSet.Find directly",
        messageFormat: "Avoid calling DbSet<TEntity>.Find(...). Use DbContext.Find<TEntity>(...) so NOF can apply tenant key rules consistently.",
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

        if (methodSymbol is null || methodSymbol.Name is not ("FindAsync" or "Find"))
        {
            return;
        }

        var containingType = methodSymbol.ContainingType;
        if (containingType is null)
        {
            return;
        }

        if (containingType is not INamedTypeSymbol namedType
            || namedType.Arity != 1
            || namedType.Name != "DbSet"
            || namedType.ContainingNamespace.ToDisplayString() != "Microsoft.EntityFrameworkCore")
        {
            return;
        }

        var diagnostic = Diagnostic.Create(
            methodSymbol.Name == "FindAsync" ? _dbSetFindAsyncDescriptor : _dbSetFindDescriptor,
            invocation.Syntax.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }
}
