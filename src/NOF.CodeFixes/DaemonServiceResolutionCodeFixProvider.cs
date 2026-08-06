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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DaemonServiceResolutionCodeFixProvider)), Shared]
public sealed class DaemonServiceResolutionCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["NOF040"];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var invocation = root?.FindNode(context.Diagnostics[0].Location.SourceSpan)
            .FirstAncestorOrSelf<InvocationExpressionSyntax>();
        var declarator = invocation?.FirstAncestorOrSelf<VariableDeclaratorSyntax>();
        if (invocation is null || declarator is null || string.IsNullOrWhiteSpace(declarator.Identifier.ValueText))
        {
            return;
        }

        var localDeclaration = invocation.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
        var usingStatement = invocation.FirstAncestorOrSelf<UsingStatementSyntax>();
        if (localDeclaration is null && usingStatement is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Resolve daemon services",
                cancellationToken => InsertResolutionAsync(
                    context.Document,
                    localDeclaration,
                    usingStatement,
                    declarator.Identifier.ValueText,
                    cancellationToken),
                equivalenceKey: "ResolveDaemonServices"),
            context.Diagnostics[0]);
    }

    private static async Task<Document> InsertResolutionAsync(
        Document document,
        LocalDeclarationStatementSyntax? localDeclaration,
        UsingStatementSyntax? usingStatement,
        string scopeName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var resolution = SyntaxFactory.ParseStatement(
                $"{scopeName}.ServiceProvider.ResolveDaemonServices();")
            .WithAdditionalAnnotations(Formatter.Annotation);

        if (localDeclaration is not null)
        {
            SyntaxNode updatedRoot = root;
            if (localDeclaration.Parent is BlockSyntax block)
            {
                var index = block.Statements.IndexOf(localDeclaration);
                updatedRoot = root.ReplaceNode(block, block.WithStatements(block.Statements.Insert(index + 1, resolution)));
            }
            else if (localDeclaration.Parent is SwitchSectionSyntax section)
            {
                var index = section.Statements.IndexOf(localDeclaration);
                updatedRoot = root.ReplaceNode(section, section.WithStatements(section.Statements.Insert(index + 1, resolution)));
            }
            else if (localDeclaration.Parent is GlobalStatementSyntax globalStatement
                && globalStatement.Parent is CompilationUnitSyntax compilationUnit)
            {
                var index = compilationUnit.Members.IndexOf(globalStatement);
                updatedRoot = root.ReplaceNode(
                    compilationUnit,
                    compilationUnit.WithMembers(
                        compilationUnit.Members.Insert(index + 1, SyntaxFactory.GlobalStatement(resolution))));
            }

            return document.WithSyntaxRoot(updatedRoot);
        }

        if (usingStatement is null)
        {
            return document;
        }

        var body = usingStatement.Statement is BlockSyntax existingBlock
            ? existingBlock.WithStatements(existingBlock.Statements.Insert(0, resolution))
            : SyntaxFactory.Block(resolution, usingStatement.Statement);
        return document.WithSyntaxRoot(root.ReplaceNode(
            usingStatement,
            usingStatement.WithStatement(body).WithAdditionalAnnotations(Formatter.Annotation)));
    }
}
