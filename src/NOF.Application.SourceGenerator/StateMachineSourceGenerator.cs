using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF.Application.SourceGenerator;

/// <summary>
/// Source generator that discovers <c>IStateMachineDefinition&lt;TState&gt;</c> implementations,
/// analyzes their <c>Build()</c> method to extract observed notification types, and generates:
/// <list type="bullet">
///   <item>Concrete notification handler classes per (definition, notification) pair</item>
///   <item>An <c>AddAllStateMachineDefinitions</c> extension method for DI registration</item>
/// </list>
/// </summary>
[Generator]
public class StateMachineSourceGenerator : IIncrementalGenerator
{
    private const string StateMachineDefinitionInterface = "NOF.Application.IStateMachineDefinition<TState>";
    // Note: IStateMachineDefinition<TState> has exactly 1 type argument (TState only)

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
                    return new DefinitionInfo(symbol, stateType, notificationTypes);
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
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();

        var sanitizedName = assemblyName.Replace(".", "");
        var initializerTypeName = $"__{sanitizedName}StateMachineHandlerAssemblyInitializer";
        sb.AppendLine("[assembly: global::NOF.Abstraction.AssemblyInitializeAttribute<global::" + assemblyName + "." + initializerTypeName + ">]");
        sb.AppendLine();

        sb.AppendLine($"namespace {assemblyName}");
        sb.AppendLine("{");

        // Collect handler class names and notification types for the static property
        var handlerPairs = new List<(string HandlerClassName, string NotificationFullName)>();

        // Generate concrete handler classes per (definition, notification) pair
        foreach (var def in definitions)
        {
            var defName = def.Symbol.Name;
            var defFullName = def.Symbol.ToDisplayString(typeFormat);
            var stateFullName = def.StateType.ToDisplayString(typeFormat);

            foreach (var notificationType in def.NotificationTypes)
            {
                var notificationName = notificationType.Name;
                var notificationFullName = notificationType.ToDisplayString(typeFormat);
                var handlerClassName = $"__{defName}_{notificationName}_Handler";

                sb.AppendLine("    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
                sb.AppendLine($"    public sealed class {handlerClassName} : global::NOF.Application.StateMachineNotificationHandler<{defFullName}, {stateFullName}, {notificationFullName}>");
                sb.AppendLine("    {");
                sb.AppendLine($"        public {handlerClassName}(");
                sb.AppendLine("            global::NOF.Application.IDbContext dbContext,");
                sb.AppendLine("            global::System.IServiceProvider serviceProvider,");
                sb.AppendLine("            global::NOF.Application.IStateMachineRegistry stateMachineRegistry)");
                sb.AppendLine("            : base(dbContext, serviceProvider, stateMachineRegistry) { }");
                sb.AppendLine("    }");
                sb.AppendLine();

                handlerPairs.Add((handlerClassName, notificationFullName));
            }
        }

        sb.AppendLine($"    internal sealed class {initializerTypeName} : global::NOF.Abstraction.IAssemblyInitializer");
        sb.AppendLine("    {");
        sb.AppendLine("        public static void Initialize(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (!services.InitializedTypes.Add(typeof({initializerTypeName})))");
        sb.AppendLine("            {");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        foreach (var (handlerClassName, notificationFullName) in handlerPairs)
        {
            var invokerTypeName = $"__{SanitizeIdentifier(handlerClassName)}NotificationInboundInvoker";
            sb.AppendLine($"            services.Add(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof({invokerTypeName}), typeof({invokerTypeName})));");
            sb.AppendLine($"            services.GetOrAddSingleton<global::NOF.Application.NotificationHandlerRegistry>().Add(new global::NOF.Application.NotificationHandlerRegistration(typeof({handlerClassName}), typeof({notificationFullName}), typeof({invokerTypeName})));");
        }
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        foreach (var (handlerClassName, notificationFullName) in handlerPairs)
        {
            var invokerTypeName = $"__{SanitizeIdentifier(handlerClassName)}NotificationInboundInvoker";
            var escapedTypeName = notificationFullName.Replace("global::", string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
            var escapedHandlerTypeName = $"{assemblyName}.{handlerClassName}".Replace("\\", "\\\\").Replace("\"", "\\\"");
            sb.AppendLine();
            sb.AppendLine($"    internal sealed class {invokerTypeName} : global::NOF.Application.INotificationInboundHandlerInvoker");
            sb.AppendLine("    {");
            sb.AppendLine($"        public string HandlerTypeName => \"{escapedHandlerTypeName}\";");
            sb.AppendLine($"        public global::System.Type HandlerType => typeof({handlerClassName});");
            sb.AppendLine($"        public string MessageTypeName => \"{escapedTypeName}\";");
            sb.AppendLine($"        public global::System.Type MessageType => typeof({notificationFullName});");
            sb.AppendLine();
            sb.AppendLine("        public object Bind(");
            sb.AppendLine("            string payloadTypeName,");
            sb.AppendLine("            global::System.ReadOnlyMemory<byte> payload,");
            sb.AppendLine("            global::System.Func<global::System.ReadOnlyMemory<byte>, global::System.Type, object?> deserialize)");
            sb.AppendLine("        {");
            sb.AppendLine("            global::System.ArgumentException.ThrowIfNullOrWhiteSpace(payloadTypeName);");
            sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(deserialize);");
            sb.AppendLine($"            if (!global::System.String.Equals(payloadTypeName, \"{escapedTypeName}\", global::System.StringComparison.Ordinal))");
            sb.AppendLine("            {");
            sb.AppendLine($"                throw new global::System.InvalidOperationException(\"Payload type '\" + payloadTypeName + \"' does not match handler message type '{escapedTypeName}'.\");");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine($"            return deserialize(payload, typeof({notificationFullName}))");
            sb.AppendLine($"                ?? throw new global::System.InvalidOperationException(\"Failed to deserialize message payload as '{escapedTypeName}'.\");");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public global::System.Threading.Tasks.ValueTask InvokeAsync(");
            sb.AppendLine("            global::System.IServiceProvider services,");
            sb.AppendLine("            object message,");
            sb.AppendLine("            global::NOF.Contract.Context context,");
            sb.AppendLine("            global::System.Threading.CancellationToken cancellationToken)");
            sb.AppendLine("        {");
            sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(services);");
            sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(message);");
            sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(context);");
            sb.AppendLine($"            var handler = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{handlerClassName}>(services);");
            sb.AppendLine($"            return new global::System.Threading.Tasks.ValueTask(handler.HandleAsync(({notificationFullName})message, context, cancellationToken));");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string SanitizeIdentifier(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return sb.ToString();
    }

    private class DefinitionInfo
    {
        public INamedTypeSymbol Symbol { get; }
        public INamedTypeSymbol StateType { get; }
        public List<INamedTypeSymbol> NotificationTypes { get; }

        public DefinitionInfo(INamedTypeSymbol symbol, INamedTypeSymbol stateType, List<INamedTypeSymbol> notificationTypes)
        {
            Symbol = symbol;
            StateType = stateType;
            NotificationTypes = notificationTypes;
        }
    }
}
