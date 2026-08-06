using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NOF.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AttributeRemovalCodeFixProvider)), Shared]
public sealed class AttributeRemovalCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["NOF020", "NOF211", "NOF212", "NOF213", "NOF216"];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (diagnostic.Id != "NOF212")
        {
            if (node.FirstAncestorOrSelf<AttributeSyntax>() is { } attribute)
            {
                RegisterRemoval(context, diagnostic, attribute, "Remove invalid attribute", "RemoveInvalidAttribute");
            }

            return;
        }

        if (node.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } declaration)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        foreach (var attribute in declaration.AttributeLists.SelectMany(static list => list.Attributes))
        {
            if (semanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
                || !IsTransportAttribute(constructor.ContainingType))
            {
                continue;
            }

            var name = constructor.ContainingType.Name;
            RegisterRemoval(context, diagnostic, attribute, $"Remove {name}", $"RemoveTransport:{name}");
        }
    }

    private static bool IsTransportAttribute(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == "NOF.Contract.TransportOverAttribute")
            {
                return true;
            }
        }

        return false;
    }

    private static void RegisterRemoval(
        CodeFixContext context,
        Diagnostic diagnostic,
        AttributeSyntax attribute,
        string title,
        string equivalenceKey)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                cancellationToken => RemoveAttributeAsync(context.Document, attribute, cancellationToken),
                equivalenceKey),
            diagnostic);
    }

    private static async Task<Document> RemoveAttributeAsync(
        Document document,
        AttributeSyntax attribute,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || attribute.Parent is not AttributeListSyntax attributeList)
        {
            return document;
        }

        SyntaxNode updatedRoot;
        if (attributeList.Attributes.Count == 1)
        {
            updatedRoot = root.RemoveNode(attributeList, SyntaxRemoveOptions.KeepExteriorTrivia) ?? root;
        }
        else
        {
            updatedRoot = root.ReplaceNode(attributeList, attributeList.WithAttributes(attributeList.Attributes.Remove(attribute)));
        }

        return document.WithSyntaxRoot(updatedRoot);
    }
}
