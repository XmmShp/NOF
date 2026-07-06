using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace NOF.Hosting.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NativeHostBuilderBuildAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _descriptor = new(
        id: "NOF401",
        title: "Do not call native host builder Build directly",
        messageFormat: "Calling '{0}.Build()' bypasses the NOF initialization pipeline. Use '{1}.{2}()' instead.",
        category: "NOF.Hosting",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly ImmutableArray<BuildMapping> _mappings =
    [
        new("NOF.Hosting.AspNetCore.NOFWebApplicationBuilder", "WebApplicationBuilder", "BuildAsync"),
        new("NOF.Hosting.Console.NOFConsoleHostBuilder", "HostApplicationBuilder", "BuildAsync"),
        new("NOF.Hosting.BlazorWebAssembly.NOFWebAssemblyHostBuilder", "WebAssemblyHostBuilder", "BuildAsync"),
        new("NOF.Hosting.Maui.NOFMauiAppBuilder", "MauiAppBuilder", "BuildAsync"),
        new("NOF.Test.NOFTestAppBuilder", "InnerBuilder", "BuildTestHostAsync")
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_descriptor];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, Microsoft.CodeAnalysis.CSharp.SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax buildAccess
            || buildAccess.Name.Identifier.ValueText != "Build")
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.Name != "Build"
            || method.Parameters.Length != 0
            || method.IsStatic)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(buildAccess.Expression, context.CancellationToken).Symbol is not IPropertySymbol property)
        {
            return;
        }

        var containingType = property.ContainingType.ToDisplayString();
        foreach (var mapping in _mappings)
        {
            if (!string.Equals(containingType, mapping.BuilderType, System.StringComparison.Ordinal)
                || !string.Equals(property.Name, mapping.NativeBuilderProperty, System.StringComparison.Ordinal))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    _descriptor,
                    buildAccess.Name.GetLocation(),
                    property.Name,
                    property.ContainingType.Name,
                    mapping.RecommendedMethod));
            return;
        }
    }

    private sealed class BuildMapping
    {
        public BuildMapping(string builderType, string nativeBuilderProperty, string recommendedMethod)
        {
            BuilderType = builderType;
            NativeBuilderProperty = nativeBuilderProperty;
            RecommendedMethod = recommendedMethod;
        }

        public string BuilderType { get; }

        public string NativeBuilderProperty { get; }

        public string RecommendedMethod { get; }
    }
}
