using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace NOF.Abstraction.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DaemonServiceResolutionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _descriptor = new(
        id: "NOF040",
        title: "Resolve daemon services after creating a scope",
        messageFormat: "Call '{0}.ServiceProvider.ResolveDaemonServices()' immediately after creating the new scope",
        category: "NOF.Abstraction",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_descriptor];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, Microsoft.CodeAnalysis.CSharp.SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocationSyntax)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocationSyntax, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol
            || !IsScopeCreationMethod(methodSymbol))
        {
            return;
        }

        if (TryGetScopeName(invocationSyntax, context.SemanticModel, context.CancellationToken, out var scopeName)
            && scopeName is not null
            && IsResolveDaemonServicesCallPresent(invocationSyntax, scopeName, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(_descriptor, invocationSyntax.GetLocation(), scopeName ?? "scope"));
    }

    private static bool IsScopeCreationMethod(IMethodSymbol methodSymbol)
    {
        if (methodSymbol.Name is not ("CreateScope" or "CreateAsyncScope"))
        {
            return false;
        }

        var returnType = methodSymbol.ReturnType;
        var returnTypeName = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return returnTypeName is "global::Microsoft.Extensions.DependencyInjection.IServiceScope"
            or "global::Microsoft.Extensions.DependencyInjection.AsyncServiceScope";
    }

    private static bool TryGetScopeName(
        InvocationExpressionSyntax invocationSyntax,
        SemanticModel _,
        CancellationToken __,
        out string? scopeName)
    {
        scopeName = null;

        if (invocationSyntax.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator })
        {
            return false;
        }

        scopeName = declarator.Identifier.ValueText;
        return !string.IsNullOrWhiteSpace(scopeName);
    }

    private static bool IsResolveDaemonServicesCallPresent(
        InvocationExpressionSyntax invocationSyntax,
        string scopeName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (!TryResolveFollowUpStatement(invocationSyntax, out var followUpStatement)
            || followUpStatement is null)
        {
            return false;
        }

        return IsResolveDaemonServicesStatement(followUpStatement, scopeName, semanticModel, cancellationToken);
    }

    private static bool TryResolveFollowUpStatement(
        InvocationExpressionSyntax invocationSyntax,
        out StatementSyntax? followUpStatement)
    {
        followUpStatement = null;

        if (invocationSyntax.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>() is { } localDeclaration)
        {
            return TryGetNextStatement(localDeclaration, out followUpStatement);
        }

        if (invocationSyntax.FirstAncestorOrSelf<UsingStatementSyntax>() is { } usingStatement)
        {
            followUpStatement = usingStatement.Statement is BlockSyntax block
                ? block.Statements.FirstOrDefault()
                : usingStatement.Statement;
            return followUpStatement is not null;
        }

        return false;
    }

    private static bool TryGetNextStatement(StatementSyntax statement, out StatementSyntax? nextStatement)
    {
        nextStatement = null;

        switch (statement.Parent)
        {
            case BlockSyntax block:
                {
                    var index = block.Statements.IndexOf(statement);
                    if (index >= 0 && index + 1 < block.Statements.Count)
                    {
                        nextStatement = block.Statements[index + 1];
                    }

                    return nextStatement is not null;
                }
            case SwitchSectionSyntax switchSection:
                {
                    var index = switchSection.Statements.IndexOf(statement);
                    if (index >= 0 && index + 1 < switchSection.Statements.Count)
                    {
                        nextStatement = switchSection.Statements[index + 1];
                    }

                    return nextStatement is not null;
                }
            default:
                return false;
        }
    }

    private static bool IsResolveDaemonServicesStatement(
        StatementSyntax statement,
        string scopeName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return semanticModel.GetOperation(statement, cancellationToken)?
            .DescendantsAndSelf()
            .OfType<IInvocationOperation>()
            .Any(invocation => IsResolveDaemonServicesInvocation(invocation, scopeName)) == true;
    }

    private static bool IsResolveDaemonServicesInvocation(IInvocationOperation invocation, string scopeName)
    {
        if (invocation.TargetMethod.Name != "ResolveDaemonServices")
        {
            return false;
        }

        var receiver = UnwrapOperation(invocation.Instance);
        if (receiver is null
            && invocation.TargetMethod.IsExtensionMethod
            && invocation.Arguments.Length > 0)
        {
            receiver = UnwrapOperation(invocation.Arguments[0].Value);
        }

        if (receiver is not IPropertyReferenceOperation
            {
                Property.Name: "ServiceProvider",
                Instance: ILocalReferenceOperation { Local.Name: var localName }
            })
        {
            return false;
        }

        return localName == scopeName;
    }

    private static IOperation? UnwrapOperation(IOperation? operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }
}
