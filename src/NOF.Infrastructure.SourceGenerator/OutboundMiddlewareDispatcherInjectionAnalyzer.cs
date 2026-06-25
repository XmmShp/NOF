using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.Infrastructure.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OutboundMiddlewareDispatcherInjectionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _descriptor = new(
        id: "NOF305",
        title: "Do not inject the outbound dispatcher directly into infrastructure outbound middleware constructors",
        messageFormat: "Constructor parameter '{0}' on outbound middleware '{1}' directly injects '{2}', which can create a resolution cycle. Use Lazy<{2}> or IServiceProvider for deferred resolution instead.",
        category: "NOF.Infrastructure",
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
            || !ImplementsInfrastructureOutboundMiddleware(typeSymbol))
        {
            return;
        }

        foreach (var constructor in typeSymbol.InstanceConstructors.Where(static ctor => !ctor.IsImplicitlyDeclared))
        {
            foreach (var parameter in constructor.Parameters)
            {
                if (!TryGetForbiddenDependency(typeSymbol, parameter.Type, out var forbiddenDependency))
                {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        _descriptor,
                        parameter.Locations.FirstOrDefault() ?? constructor.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault() ?? Location.None,
                        parameter.Name,
                        typeSymbol.Name,
                        forbiddenDependency));
            }
        }
    }

    private static bool TryGetForbiddenDependency(INamedTypeSymbol middlewareType, ITypeSymbol dependencyType, out string forbiddenDependency)
    {
        forbiddenDependency = string.Empty;

        var implementsCommandMiddleware = middlewareType.AllInterfaces.Any(static iface => iface.ToDisplayString() == "NOF.Infrastructure.ICommandOutboundMiddleware");
        var implementsNotificationMiddleware = middlewareType.AllInterfaces.Any(static iface => iface.ToDisplayString() == "NOF.Infrastructure.INotificationOutboundMiddleware");
        var dependencyName = dependencyType.ToDisplayString();

        if (implementsCommandMiddleware && dependencyName == "NOF.Application.ICommandSender")
        {
            forbiddenDependency = "ICommandSender";
            return true;
        }

        if (implementsNotificationMiddleware && dependencyName == "NOF.Application.INotificationPublisher")
        {
            forbiddenDependency = "INotificationPublisher";
            return true;
        }

        return false;
    }

    private static bool ImplementsInfrastructureOutboundMiddleware(INamedTypeSymbol typeSymbol)
        => typeSymbol.AllInterfaces.Any(static iface =>
            iface.ToDisplayString() is "NOF.Infrastructure.ICommandOutboundMiddleware" or "NOF.Infrastructure.INotificationOutboundMiddleware");
}
