using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.Domain.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValueObjectNormalizeAnalyzer : DiagnosticAnalyzer
{
    private const string InterfaceMetadataName = "NOF.Domain.IValueObject<T>";

    public static readonly DiagnosticDescriptor NormalizeShouldNotCallOf = new(
        id: "NOF013",
        title: "Normalize must not call Of",
        messageFormat: "'{0}.Normalize' must not call '{0}.Of(...)' because it causes recursive value object construction",
        category: "ValueObjectGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NormalizeShouldNotCallValidate = new(
        id: "NOF014",
        title: "Normalize should not call Validate",
        messageFormat: "'{0}.Normalize' should not call '{0}.Validate(...)' to keep normalization and validation separate",
        category: "ValueObjectGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [NormalizeShouldNotCallOf, NormalizeShouldNotCallValidate];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var methodSyntax = (MethodDeclarationSyntax)context.Node;
        if (methodSyntax.Identifier.ValueText != "Normalize")
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(methodSyntax, context.CancellationToken) is not IMethodSymbol methodSymbol ||
            !methodSymbol.IsStatic ||
            methodSymbol.ContainingType is not INamedTypeSymbol containingType ||
            !containingType.AllInterfaces.Any(i => i.OriginalDefinition.ToDisplayString() == InterfaceMetadataName))
        {
            return;
        }

        foreach (var invocation in methodSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (TryGetCalledMethodName(invocation, context, containingType) is not string methodName)
            {
                continue;
            }

            if (methodName == "Of")
            {
                context.ReportDiagnostic(Diagnostic.Create(NormalizeShouldNotCallOf, invocation.GetLocation(), containingType.Name));
            }
            else if (methodName == "Validate")
            {
                context.ReportDiagnostic(Diagnostic.Create(NormalizeShouldNotCallValidate, invocation.GetLocation(), containingType.Name));
            }
        }
    }

    private static string? TryGetCalledMethodName(
        InvocationExpressionSyntax invocation,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol containingType)
    {
        switch (invocation.Expression)
        {
            case IdentifierNameSyntax identifierName:
                return identifierName.Identifier.ValueText is "Of" or "Validate"
                    ? identifierName.Identifier.ValueText
                    : null;

            case MemberAccessExpressionSyntax memberAccess:
                if (memberAccess.Name.Identifier.ValueText is not ("Of" or "Validate"))
                {
                    return null;
                }

                var targetSymbol = context.SemanticModel.GetSymbolInfo(memberAccess.Expression, context.CancellationToken).Symbol;
                if (SymbolEqualityComparer.Default.Equals(targetSymbol, containingType))
                {
                    return memberAccess.Name.Identifier.ValueText;
                }

                return null;

            default:
                return null;
        }
    }
}
