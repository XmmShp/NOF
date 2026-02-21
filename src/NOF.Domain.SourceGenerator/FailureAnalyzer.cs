using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.Domain.SourceGenerator;

/// <summary>
/// Diagnostic analyzer for FailureAttribute usage validation
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FailureAnalyzer : DiagnosticAnalyzer
{
    // Diagnostic descriptors
    public static readonly DiagnosticDescriptor EmptyFailureName = new(
        "NOF100",
        "Empty Failure Name",
        "Failure name cannot be null or empty",
        "FailureGenerator",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor InvalidFailureCode = new(
        "NOF101",
        "Invalid Failure Code",
        "Failure code must be a non-negative integer",
        "FailureGenerator",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor DuplicateFailureNameInClass = new(
        "NOF102",
        "Duplicate Failure Name in Class",
        "Class '{0}' contains duplicate Failure name '{1}'. Consider using a different name.",
        "FailureGenerator",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor DuplicateFailureCodeInClass = new(
        "NOF103",
        "Duplicate Failure Code in Class",
        "Class '{0}' contains duplicate Failure code '{1}'. Consider using a unique code.",
        "FailureGenerator",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor NonPartialClassWithFailureAttribute = new(
        "NOF104",
        "Non-Partial Class with FailureAttribute",
        "Class '{0}' has FailureAttribute but is not marked as partial. Add the 'partial' modifier to enable source generation.",
        "FailureGenerator",
        DiagnosticSeverity.Error,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        EmptyFailureName,
        InvalidFailureCode,
        DuplicateFailureNameInClass,
        DuplicateFailureCodeInClass,
        NonPartialClassWithFailureAttribute
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeNamedTypeSymbol, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedTypeSymbol(SymbolAnalysisContext context)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
        var failureAttributes = namedTypeSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString() == "NOF.Domain.FailureAttribute")
            .ToList();

        if (failureAttributes.Count == 0)
        {
            return;
        }

        // Check if the type is partial
        var syntaxReferences = namedTypeSymbol.DeclaringSyntaxReferences;
        foreach (var syntaxRef in syntaxReferences)
        {
            if (syntaxRef.GetSyntax() is TypeDeclarationSyntax typeDeclaration)
            {
                // Check if partial modifier is missing
                if (!typeDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            NonPartialClassWithFailureAttribute,
                            typeDeclaration.GetLocation(),
                            namedTypeSymbol.Name));
                }
            }
        }

        // Validate each FailureAttribute
        var failureNames = new System.Collections.Generic.HashSet<string>();
        var failureCodes = new System.Collections.Generic.HashSet<int>();

        foreach (var attribute in failureAttributes)
        {
            var attributeSyntax = attribute.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
            if (attributeSyntax == null)
            {
                continue;
            }

            // Validate constructor arguments
            if (attribute.ConstructorArguments.Length != 3)
            {
                // If constructor arguments are invalid, skip further validation
                continue;
            }

            var nameArg = attribute.ConstructorArguments[0];
            var messageArg = attribute.ConstructorArguments[1];
            var codeArg = attribute.ConstructorArguments[2];

            // Validate name
            var name = nameArg.Value?.ToString();
            if (string.IsNullOrWhiteSpace(name))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        EmptyFailureName,
                        attributeSyntax.ArgumentList?.Arguments[0].GetLocation() ?? attributeSyntax.GetLocation()));
            }
            else
            {
                // Check for duplicate names
                if (name != null && !failureNames.Add(name))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DuplicateFailureNameInClass,
                            attributeSyntax.ArgumentList?.Arguments[0].GetLocation() ?? attributeSyntax.GetLocation(),
                            namedTypeSymbol.Name,
                            name));
                }
            }

            // Validate message (optional, but warn if empty)
            var message = messageArg.Value?.ToString();
            if (string.IsNullOrWhiteSpace(message))
            {
                // This is just a warning, not an error
            }

            // Validate failure code
            if (codeArg.Value is int code)
            {
                if (code < 0)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            InvalidFailureCode,
                            attributeSyntax.ArgumentList?.Arguments[2].GetLocation() ?? attributeSyntax.GetLocation()));
                }
                else
                {
                    // Check for duplicate codes
                    if (!failureCodes.Add(code))
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                DuplicateFailureCodeInClass,
                                attributeSyntax.ArgumentList?.Arguments[2].GetLocation() ?? attributeSyntax.GetLocation(),
                                namedTypeSymbol.Name,
                                code));
                    }
                }
            }
            else
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        InvalidFailureCode,
                        attributeSyntax.ArgumentList?.Arguments[2].GetLocation() ?? attributeSyntax.GetLocation()));
            }
        }
    }
}
