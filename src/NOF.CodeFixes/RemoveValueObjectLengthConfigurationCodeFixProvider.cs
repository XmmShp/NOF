using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace NOF.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveValueObjectLengthConfigurationCodeFixProvider)), Shared]
public sealed class RemoveValueObjectLengthConfigurationCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["NOF306", "NOF307"];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var invocation = root.FindNode(diagnostic.Location.SourceSpan)
            .FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation?.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove explicit maximum length configuration",
                cancellationToken => RemoveInvocationAsync(context.Document, invocation, memberAccess, cancellationToken),
                equivalenceKey: nameof(RemoveValueObjectLengthConfigurationCodeFixProvider)),
            diagnostic);
    }

    internal static async Task<Document> RemoveInvocationAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var replacement = memberAccess.Expression.WithTriviaFrom(invocation);
        return document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement));
    }
}
