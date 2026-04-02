using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace NOF.Infrastructure.EntityFrameworkCore.SourceGenerator;

[Generator]
public sealed class DbSetFindAsyncUsageGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor _dbSetFindAsyncDescriptor = new(
        id: "NOF300",
        title: "Do not call DbSet.FindAsync directly",
        messageFormat: "Avoid calling DbSet<TEntity>.FindAsync(...). Use DbContext.FindAsync<TEntity>(...) so NOF can apply tenant key rules consistently.",
        category: "NOF.EntityFrameworkCore",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _dbSetFindDescriptor = new(
        id: "NOF301",
        title: "Do not call DbSet.Find directly",
        messageFormat: "Avoid calling DbSet<TEntity>.Find(...). Use DbContext.Find<TEntity>(...) so NOF can apply tenant key rules consistently.",
        category: "NOF.EntityFrameworkCore",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidate(node),
                transform: static (ctx, _) => GetDiagnostic(ctx))
            .Where(static d => d is not null);

        context.RegisterSourceOutput(candidates, static (spc, diagnostic) =>
        {
            spc.ReportDiagnostic(diagnostic!);
        });
    }

    private static bool IsCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "FindAsync" } => true,
            MemberBindingExpressionSyntax { Name.Identifier.ValueText: "FindAsync" } => true,
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Find" } => true,
            MemberBindingExpressionSyntax { Name.Identifier.ValueText: "Find" } => true,
            _ => false
        };
    }

    private static Diagnostic? GetDiagnostic(GeneratorSyntaxContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return null;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol
            ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

        if (methodSymbol is null || methodSymbol.Name is not ("FindAsync" or "Find"))
        {
            return null;
        }

        var containingType = methodSymbol.ContainingType;
        if (containingType is null)
        {
            return null;
        }

        if (containingType is not INamedTypeSymbol namedType
            || namedType.Arity != 1
            || namedType.Name != "DbSet"
            || namedType.ContainingNamespace.ToDisplayString() != "Microsoft.EntityFrameworkCore")
        {
            return null;
        }

        return Diagnostic.Create(
            methodSymbol.Name == "FindAsync" ? _dbSetFindAsyncDescriptor : _dbSetFindDescriptor,
            invocation.GetLocation());
    }
}
