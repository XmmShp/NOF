using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.Hosting.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RequestOutboundMiddlewareRpcClientInjectionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _descriptor = new(
        id: "NOF400",
        title: "Do not inject RPC clients directly into outbound middleware constructors",
        messageFormat: "Constructor parameter '{0}' on outbound middleware '{1}' directly injects RPC client '{2}'. Use Lazy<{2}> or IServiceProvider for deferred resolution instead.",
        category: "NOF.Hosting",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_descriptor];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol
            || typeSymbol.TypeKind != TypeKind.Class
            || !ImplementsRequestOutboundMiddleware(typeSymbol))
        {
            return;
        }

        foreach (var constructor in typeSymbol.InstanceConstructors.Where(static ctor => !ctor.IsImplicitlyDeclared))
        {
            foreach (var parameter in constructor.Parameters)
            {
                if (!IsRpcClientType(parameter.Type))
                {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        _descriptor,
                        parameter.Locations.FirstOrDefault() ?? constructor.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault() ?? Location.None,
                        parameter.Name,
                        typeSymbol.Name,
                        parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }
    }

    private static bool ImplementsRequestOutboundMiddleware(INamedTypeSymbol typeSymbol)
        => typeSymbol.AllInterfaces.Any(static iface => iface.ToDisplayString() == "NOF.Hosting.IRequestOutboundMiddleware");

    private static bool IsRpcClientType(ITypeSymbol typeSymbol)
        => typeSymbol is INamedTypeSymbol namedType
           && (namedType.ToDisplayString() == RpcServiceHelpers.RpcClientInterfaceFqn
               || namedType.AllInterfaces.Any(static iface => iface.ToDisplayString() == RpcServiceHelpers.RpcClientInterfaceFqn));
}
