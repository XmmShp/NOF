using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Collections.Generic;
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
    private const string EquivalenceKey = "MoveValueObjectLengthToDomain";
    private static readonly FixAllProvider _fixAllProvider = new MoveValueObjectLengthToDomainFixAllProvider();

    public override ImmutableArray<string> FixableDiagnosticIds => ["NOF308"];

    public override FixAllProvider GetFixAllProvider() => _fixAllProvider;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        if (!TryGetLengthDiagnostic(diagnostic, out var candidate))
        {
            return;
        }

        var declaration = await FindValueObjectDeclarationAsync(
            context.Document.Project.Solution,
            candidate.ValueObjectType,
            context.CancellationToken).ConfigureAwait(false);
        if (declaration is null)
        {
            return;
        }

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var invocation = root?.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation?.Expression is not MemberAccessExpressionSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Move maximum length to the value object",
                cancellationToken => ApplyDiagnosticsAsync(
                    context.Document.Project.Solution,
                    [diagnostic],
                    cancellationToken),
                equivalenceKey: EquivalenceKey),
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

    private static async Task<Solution> ApplyDiagnosticsAsync(
        Solution solution,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var candidates = ImmutableArray.CreateBuilder<LengthDiagnostic>();
        foreach (var diagnostic in diagnostics)
        {
            if (TryGetLengthDiagnostic(diagnostic, out var candidate))
            {
                candidates.Add(candidate);
            }
        }

        var editsByDocument = new Dictionary<DocumentId, DocumentEdits>();
        foreach (var group in candidates.ToImmutable().GroupBy(static candidate => candidate.ValueObjectType, StringComparer.Ordinal))
        {
            var lengths = group.Select(static candidate => candidate.ConfiguredLength).Distinct().ToArray();
            if (lengths.Length != 1)
            {
                continue;
            }

            var declaration = await FindValueObjectDeclarationAsync(
                solution,
                group.Key,
                cancellationToken).ConfigureAwait(false);
            if (declaration is null)
            {
                continue;
            }

            GetOrAddEdits(editsByDocument, declaration.Value.DocumentId)
                .Declarations.Add(new DeclarationEdit(declaration.Value.Declaration, lengths[0]));

            foreach (var candidate in group)
            {
                var infrastructureDocument = candidate.Diagnostic.Location.SourceTree is { } sourceTree
                    ? solution.GetDocument(sourceTree)
                    : null;
                var root = infrastructureDocument is null
                    ? null
                    : await infrastructureDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var invocation = root?.FindNode(candidate.Diagnostic.Location.SourceSpan)
                    .FirstAncestorOrSelf<InvocationExpressionSyntax>();
                if (infrastructureDocument is null
                    || invocation?.Expression is not MemberAccessExpressionSyntax)
                {
                    continue;
                }

                GetOrAddEdits(editsByDocument, infrastructureDocument.Id).Invocations.Add(invocation);
            }
        }

        foreach (var pair in editsByDocument)
        {
            var document = solution.GetDocument(pair.Key);
            var root = document is null
                ? null
                : await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                continue;
            }

            var declarations = pair.Value.Declarations
                .GroupBy(static edit => edit.Declaration.Span)
                .Select(static group => group.First())
                .ToArray();
            var invocations = pair.Value.Invocations
                .GroupBy(static invocation => invocation.Span)
                .Select(static group => group.First())
                .OrderBy(static invocation => invocation.Span.Length)
                .ToArray();
            var updatedRoot = root.TrackNodes(
                declarations.Select(static edit => (SyntaxNode)edit.Declaration)
                    .Concat(invocations));

            foreach (var edit in declarations)
            {
                if (updatedRoot.GetCurrentNode(edit.Declaration) is not { } currentDeclaration)
                {
                    continue;
                }

                updatedRoot = updatedRoot.ReplaceNode(
                    currentDeclaration,
                    AddLengthAttribute(currentDeclaration, edit.ConfiguredLength));
            }

            foreach (var invocation in invocations)
            {
                if (updatedRoot.GetCurrentNode(invocation) is not
                    {
                        Expression: MemberAccessExpressionSyntax memberAccess
                    } currentInvocation)
                {
                    continue;
                }

                updatedRoot = updatedRoot.ReplaceNode(
                    currentInvocation,
                    memberAccess.Expression.WithTriviaFrom(currentInvocation));
            }

            solution = solution.WithDocumentSyntaxRoot(pair.Key, updatedRoot);
        }

        return solution;
    }

    private static TypeDeclarationSyntax AddLengthAttribute(
        TypeDeclarationSyntax declaration,
        int configuredLength)
    {
        var attribute = SyntaxFactory.Attribute(
            SyntaxFactory.ParseName("global::NOF.Domain.ValueObjectLength"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.NumericLiteralExpression,
                            SyntaxFactory.Literal(configuredLength))))));
        return declaration.AddAttributeLists(
                SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute)))
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static DocumentEdits GetOrAddEdits(
        Dictionary<DocumentId, DocumentEdits> editsByDocument,
        DocumentId documentId)
    {
        if (!editsByDocument.TryGetValue(documentId, out var edits))
        {
            edits = new DocumentEdits();
            editsByDocument.Add(documentId, edits);
        }

        return edits;
    }

    private static bool TryGetLengthDiagnostic(
        Diagnostic diagnostic,
        out LengthDiagnostic lengthDiagnostic)
    {
        if (diagnostic.Id == "NOF308"
            && diagnostic.Properties.TryGetValue("ValueObjectType", out var valueObjectType)
            && !string.IsNullOrWhiteSpace(valueObjectType)
            && diagnostic.Properties.TryGetValue("ConfiguredLength", out var configuredLengthText)
            && int.TryParse(configuredLengthText, NumberStyles.None, CultureInfo.InvariantCulture, out var configuredLength)
            && configuredLength > 0)
        {
            lengthDiagnostic = new LengthDiagnostic(diagnostic, valueObjectType!, configuredLength);
            return true;
        }

        lengthDiagnostic = null!;
        return false;
    }

    private static async Task<ImmutableArray<Diagnostic>> GetFixAllDiagnosticsAsync(FixAllContext context)
    {
        switch (context.Scope)
        {
            case FixAllScope.Document when context.Document is not null:
                return [.. await context.GetDocumentDiagnosticsAsync(context.Document).ConfigureAwait(false)];
            case FixAllScope.Project:
                return [.. await context.GetAllDiagnosticsAsync(context.Project).ConfigureAwait(false)];
            case FixAllScope.Solution:
                var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
                foreach (var project in context.Solution.Projects)
                {
                    diagnostics.AddRange(await context.GetAllDiagnosticsAsync(project).ConfigureAwait(false));
                }

                return diagnostics.ToImmutable();
            default:
                return [];
        }
    }

    private sealed class MoveValueObjectLengthToDomainFixAllProvider : FixAllProvider
    {
        public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            var diagnostics = await GetFixAllDiagnosticsAsync(fixAllContext).ConfigureAwait(false);
            if (diagnostics.IsEmpty
                || !await HasApplicableDiagnosticsAsync(
                    fixAllContext.Solution,
                    diagnostics,
                    fixAllContext.CancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return CodeAction.Create(
                "Move all maximum lengths to their value objects",
                cancellationToken => ApplyDiagnosticsAsync(
                    fixAllContext.Solution,
                    diagnostics,
                    cancellationToken),
                equivalenceKey: EquivalenceKey);
        }
    }

    private static async Task<bool> HasApplicableDiagnosticsAsync(
        Solution solution,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var candidates = diagnostics
            .Select(diagnostic => TryGetLengthDiagnostic(diagnostic, out var candidate) ? candidate : null)
            .Where(static candidate => candidate is not null)
            .Cast<LengthDiagnostic>();
        foreach (var group in candidates.GroupBy(static candidate => candidate.ValueObjectType, StringComparer.Ordinal))
        {
            if (group.Select(static candidate => candidate.ConfiguredLength).Distinct().Take(2).Count() != 1
                || await FindValueObjectDeclarationAsync(solution, group.Key, cancellationToken).ConfigureAwait(false) is null)
            {
                continue;
            }

            if (group.Any(candidate => candidate.Diagnostic.Location.SourceTree is { } sourceTree
                && solution.GetDocument(sourceTree) is not null))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class DocumentEdits
    {
        public List<DeclarationEdit> Declarations { get; } = [];

        public List<InvocationExpressionSyntax> Invocations { get; } = [];
    }

    private sealed class DeclarationEdit
    {
        public DeclarationEdit(TypeDeclarationSyntax declaration, int configuredLength)
        {
            Declaration = declaration;
            ConfiguredLength = configuredLength;
        }

        public TypeDeclarationSyntax Declaration { get; }

        public int ConfiguredLength { get; }
    }

    private sealed class LengthDiagnostic
    {
        public LengthDiagnostic(Diagnostic diagnostic, string valueObjectType, int configuredLength)
        {
            Diagnostic = diagnostic;
            ValueObjectType = valueObjectType;
            ConfiguredLength = configuredLength;
        }

        public Diagnostic Diagnostic { get; }

        public string ValueObjectType { get; }

        public int ConfiguredLength { get; }
    }
}
