using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NOF.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MoveValueObjectLengthToDomainCodeFixProvider)), Shared]
public sealed class MoveValueObjectLengthToDomainCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["NOF308"];

    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue("ValueObjectType", out var valueObjectType)
            || string.IsNullOrWhiteSpace(valueObjectType)
            || !diagnostic.Properties.TryGetValue("ConfiguredLength", out var configuredLengthText)
            || !int.TryParse(configuredLengthText, NumberStyles.None, CultureInfo.InvariantCulture, out var configuredLength)
            || configuredLength <= 0)
        {
            return;
        }

        var declaration = await FindValueObjectDeclarationAsync(
            context.Document.Project.Solution,
            valueObjectType!,
            context.CancellationToken).ConfigureAwait(false);
        if (declaration is null)
        {
            return;
        }

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var invocation = root?.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation?.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Move maximum length to the value object",
                cancellationToken => MoveLengthAsync(
                    context.Document,
                    invocation,
                    memberAccess,
                    declaration.Value.DocumentId,
                    declaration.Value.Declaration,
                    configuredLength,
                    cancellationToken),
                equivalenceKey: "MoveValueObjectLengthToDomain"),
            diagnostic);
    }

    private static async Task<(DocumentId DocumentId, TypeDeclarationSyntax Declaration)?> FindValueObjectDeclarationAsync(
        Solution solution,
        string fullyQualifiedType,
        CancellationToken cancellationToken)
    {
        var simpleName = fullyQualifiedType.Split('.').Last();
        INamedTypeSymbol? symbol = null;
        foreach (var project in solution.Projects)
        {
            var symbols = await SymbolFinder.FindDeclarationsAsync(
                project,
                simpleName,
                ignoreCase: false,
                SymbolFilter.Type,
                cancellationToken).ConfigureAwait(false);
            symbol = symbols.OfType<INamedTypeSymbol>().FirstOrDefault(candidate =>
                candidate.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == fullyQualifiedType);
            if (symbol is not null)
            {
                break;
            }
        }

        var syntaxReference = symbol?.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference is null
            || await syntaxReference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false) is not TypeDeclarationSyntax declaration)
        {
            return null;
        }

        var document = solution.GetDocument(declaration.SyntaxTree);
        return document is null ? null : (document.Id, declaration);
    }

    private static async Task<Solution> MoveLengthAsync(
        Document infrastructureDocument,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        DocumentId valueObjectDocumentId,
        TypeDeclarationSyntax valueObjectDeclaration,
        int configuredLength,
        CancellationToken cancellationToken)
    {
        var solution = infrastructureDocument.Project.Solution;
        var infrastructureRoot = await infrastructureDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var valueObjectDocument = solution.GetDocument(valueObjectDocumentId);
        var valueObjectRoot = valueObjectDocument is null
            ? null
            : await valueObjectDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (infrastructureRoot is null || valueObjectDocument is null || valueObjectRoot is null)
        {
            return solution;
        }

        var attribute = SyntaxFactory.Attribute(
            SyntaxFactory.ParseName("global::NOF.Domain.ValueObjectLength"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.NumericLiteralExpression,
                            SyntaxFactory.Literal(configuredLength))))));
        if (valueObjectDocumentId == infrastructureDocument.Id)
        {
            var trackedRoot = infrastructureRoot.TrackNodes(valueObjectDeclaration, invocation);
            var currentValueObject = trackedRoot.GetCurrentNode(valueObjectDeclaration)!;
            var updatedRoot = trackedRoot.ReplaceNode(
                currentValueObject,
                AddLengthAttribute(currentValueObject, attribute));
            var currentInvocation = updatedRoot.GetCurrentNode(invocation)!;
            updatedRoot = updatedRoot.ReplaceNode(
                currentInvocation,
                ((MemberAccessExpressionSyntax)currentInvocation.Expression).Expression.WithTriviaFrom(currentInvocation));
            return solution.WithDocumentSyntaxRoot(infrastructureDocument.Id, updatedRoot);
        }

        var updatedValueObjectRoot = valueObjectRoot.ReplaceNode(
            valueObjectDeclaration,
            AddLengthAttribute(valueObjectDeclaration, attribute));
        var updatedInfrastructureRoot = infrastructureRoot.ReplaceNode(
            invocation,
            memberAccess.Expression.WithTriviaFrom(invocation));

        return solution
            .WithDocumentSyntaxRoot(valueObjectDocumentId, updatedValueObjectRoot)
            .WithDocumentSyntaxRoot(infrastructureDocument.Id, updatedInfrastructureRoot);
    }

    private static TypeDeclarationSyntax AddLengthAttribute(
        TypeDeclarationSyntax declaration,
        AttributeSyntax attribute)
        => declaration.AddAttributeLists(
                SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute)))
            .WithAdditionalAnnotations(Formatter.Annotation);
}
