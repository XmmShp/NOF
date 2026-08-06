using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace NOF.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ValueObjectOrderingCodeFixProvider)), Shared]
public sealed class ValueObjectOrderingCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["NOF015"];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue("PrimitiveType", out var primitiveType)
            || string.IsNullOrWhiteSpace(primitiveType)
            || diagnostic.Properties.TryGetValue("NullableKey", out var nullableKey) && nullableKey == "true")
        {
            return;
        }

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var node = root?.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var lambda = node?.FirstAncestorOrSelf<LambdaExpressionSyntax>() ?? node as LambdaExpressionSyntax;
        if (lambda?.Body is not ExpressionSyntax body)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Cast ordering key to {primitiveType}",
                cancellationToken => AddCastAsync(context.Document, lambda, body, primitiveType!, cancellationToken),
                equivalenceKey: "CastOrderingKeyToPrimitive"),
            diagnostic);
    }

    private static async Task<Document> AddCastAsync(
        Document document,
        LambdaExpressionSyntax lambda,
        ExpressionSyntax body,
        string primitiveType,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var cast = SyntaxFactory.CastExpression(
                SyntaxFactory.ParseTypeName(primitiveType),
                body.WithoutTrivia())
            .WithTriviaFrom(body)
            .WithAdditionalAnnotations(Formatter.Annotation);
        var updatedLambda = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.WithExpressionBody(cast),
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.WithExpressionBody(cast),
            _ => lambda,
        };
        return document.WithSyntaxRoot(root.ReplaceNode(lambda, updatedLambda));
    }
}
