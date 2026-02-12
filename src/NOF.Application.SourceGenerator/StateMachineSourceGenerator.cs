using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF.Application.SourceGenerator;

/// <summary>
/// Source generator that discovers <c>IStateMachineDefinition&lt;TState, TContext&gt;</c> implementations,
/// analyzes their <c>Build()</c> method to extract observed notification types, and generates:
/// <list type="bullet">
///   <item>Concrete notification handler classes per (definition, notification) pair</item>
///   <item>An <c>AddAllStateMachineDefinitions</c> extension method for DI registration</item>
/// </list>
/// </summary>
[Generator]
public class StateMachineSourceGenerator : IIncrementalGenerator
{
    private const string StateMachineDefinitionInterface = "NOF.Application.IStateMachineDefinition<TState, TContext>";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find classes implementing IStateMachineDefinition<,> in source
        var definitionClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, _) =>
                {
                    if (ctx.Node is not ClassDeclarationSyntax cds)
                    {
                        return null;
                    }
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(cds);
                    if (symbol is not { IsAbstract: false, IsGenericType: false })
                    {
                        return null;
                    }
                    var smIface = GetStateMachineDefinitionInterface(symbol);
                    if (smIface is null)
                    {
                        return null;
                    }
                    var stateType = (INamedTypeSymbol)smIface.TypeArguments[0];
                    var contextType = (INamedTypeSymbol)smIface.TypeArguments[1];
                    // Extract notification types from Build() method body
                    var buildMethod = cds.Members
                        .OfType<MethodDeclarationSyntax>()
                        .FirstOrDefault(m => m.Identifier.Text == "Build");
                    if (buildMethod?.Body is null && buildMethod?.ExpressionBody is null)
                    {
                        return null;
                    }
                    var notificationTypes = ExtractNotificationTypes(buildMethod, ctx.SemanticModel);
                    if (notificationTypes.Count == 0)
                    {
                        return null;
                    }
                    return new DefinitionInfo(symbol, stateType, contextType, notificationTypes);
                })
            .Where(static s => s is not null);

        // Combine with compilation for assembly name
        var allDefinitions = context.CompilationProvider
            .Combine(definitionClasses.Collect())
            .Select(static (data, _) =>
            {
                var (compilation, definitions) = data;
                var assemblyName = compilation.AssemblyName ?? "Unknown";
                return (AssemblyName: assemblyName, Definitions: definitions);
            });

        // Generate code
        context.RegisterSourceOutput(allDefinitions, static (spc, data) =>
        {
            var validDefinitions = data.Definitions
                .Where(d => d is not null)
                .Cast<DefinitionInfo>()
                .ToImmutableArray();

            if (validDefinitions.IsEmpty)
            {
                return;
            }

            var source = GenerateCode(data.AssemblyName, validDefinitions);
            spc.AddSource("StateMachineNotificationHandlers.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static INamedTypeSymbol? GetStateMachineDefinitionInterface(INamedTypeSymbol symbol)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            if (!iface.IsGenericType)
            {
                continue;
            }
            if (iface.OriginalDefinition.ToDisplayString() == StateMachineDefinitionInterface)
            {
                return iface;
            }
        }
        return null;
    }

    /// <summary>
    /// Walks the Build() method body to find all generic invocations of Correlate, StartWhen, and When,
    /// and extracts their type arguments as notification type symbols.
    /// </summary>
    private static List<INamedTypeSymbol> ExtractNotificationTypes(MethodDeclarationSyntax buildMethod, SemanticModel semanticModel)
    {
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var result = new List<INamedTypeSymbol>();

        SyntaxNode? body = (SyntaxNode?)buildMethod.Body ?? buildMethod.ExpressionBody;
        if (body is null)
        {
            return result;
        }

        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            GenericNameSyntax? genericName = null;

            // Handle: builder.Correlate<T>(...), builder.StartWhen<T>(...), .When<T>()
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name is GenericNameSyntax gn)
            {
                genericName = gn;
            }

            if (genericName is null)
            {
                continue;
            }

            var methodName = genericName.Identifier.Text;
            if (methodName != "Correlate" && methodName != "StartWhen" && methodName != "When")
            {
                continue;
            }

            if (genericName.TypeArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            var typeArg = genericName.TypeArgumentList.Arguments[0];
            var typeInfo = semanticModel.GetTypeInfo(typeArg);
            if (typeInfo.Type is INamedTypeSymbol namedType && seen.Add(namedType))
            {
                result.Add(namedType);
            }
        }

        return result;
    }

    private static string GenerateCode(string assemblyName, ImmutableArray<DefinitionInfo> definitions)
    {
        var typeFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        sb.AppendLine($"namespace {assemblyName}");
        sb.AppendLine("{");

        // Generate concrete handler classes per (definition, notification) pair
        foreach (var def in definitions)
        {
            var defName = def.Symbol.Name;
            var defFullName = def.Symbol.ToDisplayString(typeFormat);
            var stateFullName = def.StateType.ToDisplayString(typeFormat);
            var contextFullName = def.ContextType.ToDisplayString(typeFormat);

            foreach (var notificationType in def.NotificationTypes)
            {
                var notificationName = notificationType.Name;
                var notificationFullName = notificationType.ToDisplayString(typeFormat);
                var handlerClassName = $"__{defName}_{notificationName}_Handler";

                sb.AppendLine($"    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
                sb.AppendLine($"    public sealed class {handlerClassName} : global::NOF.Application.StateMachineNotificationHandler<{defFullName}, {stateFullName}, {contextFullName}, {notificationFullName}>");
                sb.AppendLine("    {");
                sb.AppendLine($"        public {handlerClassName}(");
                sb.AppendLine($"            global::NOF.Application.IStateMachineContextRepository repository,");
                sb.AppendLine($"            global::NOF.Application.IUnitOfWork uow,");
                sb.AppendLine($"            global::System.IServiceProvider serviceProvider,");
                sb.AppendLine($"            global::NOF.Application.IStateMachineRegistry stateMachineRegistry)");
                sb.AppendLine("            : base(repository, uow, serviceProvider, stateMachineRegistry) { }");
                sb.AppendLine("    }");
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private class DefinitionInfo
    {
        public INamedTypeSymbol Symbol { get; }
        public INamedTypeSymbol StateType { get; }
        public INamedTypeSymbol ContextType { get; }
        public List<INamedTypeSymbol> NotificationTypes { get; }

        public DefinitionInfo(INamedTypeSymbol symbol, INamedTypeSymbol stateType, INamedTypeSymbol contextType, List<INamedTypeSymbol> notificationTypes)
        {
            Symbol = symbol;
            StateType = stateType;
            ContextType = contextType;
            NotificationTypes = notificationTypes;
        }
    }
}
