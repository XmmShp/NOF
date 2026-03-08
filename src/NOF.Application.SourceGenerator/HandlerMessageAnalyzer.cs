using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace NOF.Application.SourceGenerator;

/// <summary>
/// Analyzer that enforces:
/// 1. A handler class can only implement one handler interface (ICommandHandler, IEventHandler, INotificationHandler, IRequestHandler).
/// 2. A message class can only implement one message interface (ICommand, IEvent, INotification, IRequest/IRequest&lt;T&gt;).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class HandlerMessageAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] HandlerInterfaceNames =
    {
        "NOF.Application.ICommandHandler<TCommand>",
        "NOF.Application.IEventHandler<TEvent>",
        "NOF.Application.INotificationHandler<TNotification>",
        "NOF.Application.IRequestHandler<TRequest>",
        "NOF.Application.IRequestHandler<TRequest, TResponse>"
    };

    private static readonly string[] MessageInterfaceNames =
    {
        "NOF.Contract.ICommand",
        "NOF.Contract.IEvent",
        "NOF.Contract.INotification",
        "NOF.Contract.IRequest",
        "NOF.Contract.IRequest<TResponse>"
    };

    public static readonly DiagnosticDescriptor MultipleHandlerInterfacesRule = new(
        id: "NOF001",
        title: "Handler implements multiple handler interfaces",
        messageFormat: "Handler '{0}' implements multiple handler interfaces: {1}. A handler class must implement exactly one handler interface.",
        category: "NOF.Application",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MultipleMessageInterfacesRule = new(
        id: "NOF002",
        title: "Message implements multiple message interfaces",
        messageFormat: "Message '{0}' implements multiple message interfaces: {1}. A message class must implement exactly one message interface.",
        category: "NOF.Application",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MultipleHandlerInterfacesRule, MultipleMessageInterfacesRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var symbol = (INamedTypeSymbol)context.Symbol;
        if (symbol.IsAbstract || symbol.TypeKind != TypeKind.Class)
        {
            return;
        }

        CheckHandlerInterfaces(context, symbol);
        CheckMessageInterfaces(context, symbol);
    }

    private static void CheckHandlerInterfaces(SymbolAnalysisContext context, INamedTypeSymbol symbol)
    {
        var matched = new List<string>();

        foreach (var iface in symbol.AllInterfaces)
        {
            if (!iface.IsGenericType)
            {
                continue;
            }

            var display = iface.OriginalDefinition.ToDisplayString();
            foreach (var name in HandlerInterfaceNames)
            {
                if (display == name)
                {
                    matched.Add(FriendlyHandlerName(name));
                    break;
                }
            }
        }

        if (matched.Count > 1)
        {
            var diagnostic = Diagnostic.Create(
                MultipleHandlerInterfacesRule,
                symbol.Locations.FirstOrDefault(),
                symbol.Name,
                string.Join(", ", matched));
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void CheckMessageInterfaces(SymbolAnalysisContext context, INamedTypeSymbol symbol)
    {
        var matched = new List<string>();

        foreach (var iface in symbol.AllInterfaces)
        {
            var display = iface.IsGenericType
                ? iface.OriginalDefinition.ToDisplayString()
                : iface.ToDisplayString();

            foreach (var name in MessageInterfaceNames)
            {
                if (display == name)
                {
                    matched.Add(FriendlyMessageName(name));
                    break;
                }
            }
        }

        if (matched.Count > 1)
        {
            var diagnostic = Diagnostic.Create(
                MultipleMessageInterfacesRule,
                symbol.Locations.FirstOrDefault(),
                symbol.Name,
                string.Join(", ", matched));
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static string FriendlyHandlerName(string fullName)
    {
        // "NOF.Application.ICommandHandler<TCommand>" → "ICommandHandler"
        var start = fullName.LastIndexOf('.') + 1;
        var end = fullName.IndexOf('<');
        return end > start ? fullName.Substring(start, end - start) : fullName.Substring(start);
    }

    private static string FriendlyMessageName(string fullName)
    {
        // "NOF.Contract.ICommand" → "ICommand"
        var start = fullName.LastIndexOf('.') + 1;
        var end = fullName.IndexOf('<');
        return end > start ? fullName.Substring(start, end - start) : fullName.Substring(start);
    }
}
