using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NOF.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NativeHostBuilderBuildCodeFixProvider)), Shared]
public sealed class NativeHostBuilderBuildCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["NOF401"];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue("RecommendedMethod", out var recommendedMethod)
            || string.IsNullOrWhiteSpace(recommendedMethod))
        {
            return;
        }

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var invocation = root?.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation?.Expression is not MemberAccessExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax nativeBuilderAccess
            })
        {
            return;
        }

        if (!CanAwait(invocation))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Use {recommendedMethod}",
                cancellationToken => ReplaceBuildAsync(
                    context.Document,
                    invocation,
                    nativeBuilderAccess.Expression,
                    recommendedMethod!,
                    cancellationToken),
                equivalenceKey: "UseNofBuildAsync"),
            diagnostic);
    }

    private static bool CanAwait(SyntaxNode invocation)
    {
        foreach (var ancestor in invocation.Ancestors())
        {
            switch (ancestor)
            {
                case AnonymousFunctionExpressionSyntax anonymousFunction:
                    return anonymousFunction.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
                case LocalFunctionStatementSyntax localFunction:
                    return localFunction.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.AsyncKeyword));
                case MethodDeclarationSyntax method:
                    return method.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.AsyncKeyword));
                case GlobalStatementSyntax:
                    return true;
            }
        }

        return false;
    }

    private static async Task<Document> ReplaceBuildAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax nofBuilder,
        string recommendedMethod,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var replacement = SyntaxFactory.AwaitExpression(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        nofBuilder.WithoutTrivia(),
                        SyntaxFactory.IdentifierName(recommendedMethod))))
            .WithTriviaFrom(invocation)
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement));
    }
}
