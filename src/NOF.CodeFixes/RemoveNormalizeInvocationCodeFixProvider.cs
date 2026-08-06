using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace NOF.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveNormalizeInvocationCodeFixProvider)), Shared]
public sealed class RemoveNormalizeInvocationCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["NOF013", "NOF014"];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics[0];
        var invocation = root?.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation?.Parent is not ExpressionStatementSyntax statement || statement.Expression != invocation)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove invocation from Normalize",
                cancellationToken => RemoveStatementAsync(context.Document, statement, cancellationToken),
                equivalenceKey: "RemoveInvocationFromNormalize"),
            diagnostic);
    }

    private static async Task<Document> RemoveStatementAsync(
        Document document,
        ExpressionStatementSyntax statement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var updatedRoot = root?.RemoveNode(statement, SyntaxRemoveOptions.KeepExteriorTrivia);
        return updatedRoot is null ? document : document.WithSyntaxRoot(updatedRoot);
    }
}
