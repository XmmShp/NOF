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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TypeDeclarationCodeFixProvider)), Shared]
public sealed class TypeDeclarationCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ["NOF010", "NOF011", "NOF021", "NOF104", "NOF200", "NOF202", "NOF209", "NOF211", "NOF300", "NOF304"];

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
        switch (diagnostic.Id)
        {
            case "NOF010":
            case "NOF104":
            case "NOF300":
                RegisterModifiersFix(context, diagnostic, node, [SyntaxKind.PartialKeyword], "Add partial modifier");
                break;
            case "NOF021":
                RegisterModifiersFix(context, diagnostic, node, [SyntaxKind.StaticKeyword, SyntaxKind.PartialKeyword], "Make class partial static");
                break;
            case "NOF011":
                await RegisterNullableUnderlyingTypeFixAsync(context, diagnostic, node).ConfigureAwait(false);
                break;
            case "NOF200":
                if (node.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } valueType
                    && valueType is StructDeclarationSyntax or RecordDeclarationSyntax)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Convert request to a reference type",
                            cancellationToken => ConvertRequestToReferenceTypeAsync(context.Document, valueType, cancellationToken),
                            equivalenceKey: "ConvertRequestToReferenceType"),
                        diagnostic);
                }
                break;
            case "NOF202":
                if (node.FirstAncestorOrSelf<ClassDeclarationSyntax>() is { } requestClass)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Add public parameterless constructor",
                            cancellationToken => AddParameterlessConstructorAsync(context.Document, requestClass, cancellationToken),
                            equivalenceKey: "AddPublicParameterlessConstructor"),
                        diagnostic);
                }
                break;
            case "NOF209":
                if (node.FirstAncestorOrSelf<MethodDeclarationSyntax>() is { } method)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Return NOF Result",
                            cancellationToken => ReplaceVoidReturnAsync(context.Document, method, cancellationToken),
                            equivalenceKey: "ReturnNofResult"),
                        diagnostic);
                }
                break;
            case "NOF211":
                if (node.FirstAncestorOrSelf<InterfaceDeclarationSyntax>() is { } serviceInterface)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Make interface an RPC service contract",
                            cancellationToken => AddRpcServiceBaseAsync(context.Document, serviceInterface, cancellationToken),
                            equivalenceKey: "AddIRpcService"),
                        diagnostic);
                }
                break;
            case "NOF304":
                await RegisterNofDbContextFixAsync(context, diagnostic, node).ConfigureAwait(false);
                break;
        }
    }

    private static void RegisterModifiersFix(
        CodeFixContext context,
        Diagnostic diagnostic,
        SyntaxNode node,
        ImmutableArray<SyntaxKind> modifierKinds,
        string title)
    {
        if (node.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } declaration)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                cancellationToken => AddModifiersAsync(context.Document, declaration, modifierKinds, cancellationToken),
                equivalenceKey: title),
            diagnostic);
    }

    private static async Task RegisterNullableUnderlyingTypeFixAsync(
        CodeFixContext context,
        Diagnostic diagnostic,
        SyntaxNode node)
    {
        if (node.FirstAncestorOrSelf<StructDeclarationSyntax>() is not { BaseList: { } baseList })
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var nullableType = baseList.Types
            .Where(baseType => semanticModel.GetTypeInfo(baseType.Type, context.CancellationToken).Type is INamedTypeSymbol namedType
                && namedType.OriginalDefinition.ToDisplayString() == "NOF.Domain.IValueObject<T>")
            .SelectMany(static baseType => baseType.Type.DescendantNodesAndSelf().OfType<NullableTypeSyntax>())
            .FirstOrDefault();
        if (nullableType is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use a non-nullable underlying type",
                cancellationToken => ReplaceNodeAsync(
                    context.Document,
                    nullableType,
                    nullableType.ElementType.WithTriviaFrom(nullableType),
                    cancellationToken),
                equivalenceKey: "UseNonNullableValueObjectUnderlyingType"),
            diagnostic);
    }

    private static async Task RegisterNofDbContextFixAsync(
        CodeFixContext context,
        Diagnostic diagnostic,
        SyntaxNode node)
    {
        if (node.FirstAncestorOrSelf<ClassDeclarationSyntax>() is not { BaseList: { } baseList } declaration)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var directDbContextBase = baseList.Types.FirstOrDefault(baseType =>
        {
            var type = semanticModel.GetTypeInfo(baseType.Type, context.CancellationToken).Type;
            return type is INamedTypeSymbol namedType
                && namedType.Name == "DbContext"
                && namedType.Arity == 0
                && namedType.ContainingNamespace.ToDisplayString() == "Microsoft.EntityFrameworkCore";
        });
        if (directDbContextBase is null)
        {
            return;
        }

        if (!CanConstructNofDbContext(declaration, directDbContextBase, semanticModel, context.CancellationToken))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Inherit NOFDbContext",
                cancellationToken => ReplaceNodeAsync(
                    context.Document,
                    directDbContextBase.Type,
                    SyntaxFactory.ParseTypeName("global::NOF.Infrastructure.EntityFrameworkCore.NOFDbContext")
                        .WithTriviaFrom(directDbContextBase.Type)
                        .WithAdditionalAnnotations(Formatter.Annotation),
                    cancellationToken),
                equivalenceKey: "InheritNOFDbContext"),
            diagnostic);
    }

    private static bool CanConstructNofDbContext(
        ClassDeclarationSyntax declaration,
        BaseTypeSyntax directDbContextBase,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (directDbContextBase is PrimaryConstructorBaseTypeSyntax primaryConstructorBase)
        {
            return primaryConstructorBase.ArgumentList.Arguments.Count == 1
                && IsDbContextOptions(
                    semanticModel.GetTypeInfo(primaryConstructorBase.ArgumentList.Arguments[0].Expression, cancellationToken).ConvertedType);
        }

        var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>().ToArray();
        return constructors.Length > 0 && constructors.All(constructor =>
            constructor.Initializer is { } initializer
            && (initializer.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword)
                || initializer.ArgumentList.Arguments.Count == 1
                && IsDbContextOptions(
                    semanticModel.GetTypeInfo(initializer.ArgumentList.Arguments[0].Expression, cancellationToken).ConvertedType)));
    }

    private static bool IsDbContextOptions(ITypeSymbol? type)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (current.Name == "DbContextOptions"
                && current.ContainingNamespace.ToDisplayString() == "Microsoft.EntityFrameworkCore")
            {
                return true;
            }
        }

        return false;
    }

    private static Task<Document> AddModifiersAsync(
        Document document,
        TypeDeclarationSyntax declaration,
        ImmutableArray<SyntaxKind> modifierKinds,
        CancellationToken cancellationToken)
    {
        var updated = declaration;
        foreach (var modifierKind in modifierKinds)
        {
            if (!updated.Modifiers.Any(modifier => modifier.IsKind(modifierKind)))
            {
                updated = updated.AddModifiers(SyntaxFactory.Token(modifierKind));
            }
        }

        return ReplaceNodeAsync(document, declaration, updated.WithAdditionalAnnotations(Formatter.Annotation), cancellationToken);
    }

    private static Task<Document> ConvertRequestToReferenceTypeAsync(
        Document document,
        TypeDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        TypeDeclarationSyntax updated;
        if (declaration is RecordDeclarationSyntax recordDeclaration)
        {
            updated = recordDeclaration.WithClassOrStructKeyword(
                SyntaxFactory.Token(SyntaxKind.ClassKeyword).WithTriviaFrom(recordDeclaration.ClassOrStructKeyword));
        }
        else
        {
            var declarationText = declaration.ToFullString();
            var keywordOffset = declaration.Keyword.SpanStart - declaration.FullSpan.Start;
            var classText = declarationText.Remove(keywordOffset, declaration.Keyword.Span.Length).Insert(keywordOffset, "class");
            updated = (TypeDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(classText)!;
        }

        updated = updated.WithModifiers(SyntaxFactory.TokenList(
            updated.Modifiers.Where(static modifier => !modifier.IsKind(SyntaxKind.ReadOnlyKeyword)
                && !modifier.IsKind(SyntaxKind.RefKeyword))));
        return ReplaceNodeAsync(document, declaration, updated.WithAdditionalAnnotations(Formatter.Annotation), cancellationToken);
    }

    private static Task<Document> AddParameterlessConstructorAsync(
        Document document,
        ClassDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        var constructor = SyntaxFactory.ConstructorDeclaration(declaration.Identifier)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithBody(SyntaxFactory.Block())
            .WithAdditionalAnnotations(Formatter.Annotation);
        return ReplaceNodeAsync(
            document,
            declaration,
            declaration.AddMembers(constructor).WithAdditionalAnnotations(Formatter.Annotation),
            cancellationToken);
    }

    private static Task<Document> ReplaceVoidReturnAsync(
        Document document,
        MethodDeclarationSyntax method,
        CancellationToken cancellationToken)
        => ReplaceNodeAsync(
            document,
            method,
            method.WithReturnType(
                SyntaxFactory.ParseTypeName("global::NOF.Contract.Result")
                    .WithTriviaFrom(method.ReturnType)
                    .WithAdditionalAnnotations(Formatter.Annotation)),
            cancellationToken);

    private static Task<Document> AddRpcServiceBaseAsync(
        Document document,
        InterfaceDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        var baseType = SyntaxFactory.SimpleBaseType(
            SyntaxFactory.ParseTypeName("global::NOF.Contract.IRpcService"));
        var baseList = declaration.BaseList is null
            ? SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(baseType))
            : declaration.BaseList.AddTypes(baseType);
        return ReplaceNodeAsync(
            document,
            declaration,
            declaration.WithBaseList(baseList).WithAdditionalAnnotations(Formatter.Annotation),
            cancellationToken);
    }

    private static async Task<Document> ReplaceNodeAsync(
        Document document,
        SyntaxNode oldNode,
        SyntaxNode newNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return root is null ? document : document.WithSyntaxRoot(root.ReplaceNode(oldNode, newNode));
    }
}
