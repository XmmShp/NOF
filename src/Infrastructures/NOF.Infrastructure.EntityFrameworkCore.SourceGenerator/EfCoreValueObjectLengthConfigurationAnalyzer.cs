using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NOF.SourceGenerator.Shared;
using System.Collections.Immutable;

namespace NOF.Infrastructure.EntityFrameworkCore.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EfCoreValueObjectLengthConfigurationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ValueObjectLengthConfigurationAnalysis.SupportedDiagnostics;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(
            static operationContext => ValueObjectLengthConfigurationAnalysis.AnalyzeInvocation(
                operationContext,
                ValueObjectLengthBuilderFamily.EntityFrameworkCore),
            OperationKind.Invocation);
    }
}
