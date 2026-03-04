using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF.Hosting.SourceGenerator;

/// <summary>
/// Source generator: detects handler classes implementing ICommandHandler, IEventHandler,
/// INotificationHandler, IRequestHandler from the current project and prefix-matching referenced
/// assemblies, and generates an <c>AddAllHandlers</c> extension method that registers them
/// into <c>HandlerInfos</c> via DI.
/// </summary>
[Generator]
public class HandlerRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Extract handler classes from current project source code (syntax + semantic)
        var sourceClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, _) =>
                {
                    if (ctx.Node is not ClassDeclarationSyntax cds)
                    {
                        return null;
                    }
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(cds);
                    if (symbol is { IsAbstract: false, IsGenericType: false } && IsHandlerType(symbol))
                    {
                        return symbol;
                    }
                    return null;
                })
            .Where(static s => s is not null);

        // 2. Extract handler classes from prefix-matching referenced assemblies (metadata)
        var referencedTypes = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                var currentAssemblyName = compilation.AssemblyName ?? string.Empty;
                var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
                foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
                {
                    var assemblyName = assembly.Name;

                    // Only scan assemblies whose name starts with the current assembly name prefix
                    if (!string.IsNullOrEmpty(currentAssemblyName) &&
                        assemblyName.StartsWith(currentAssemblyName) &&
                        (assemblyName.Length == currentAssemblyName.Length || assemblyName[currentAssemblyName.Length] == '.'))
                    {
                        CollectHandlerTypes(assembly.GlobalNamespace, builder);
                    }
                }
                return builder.ToImmutable();
            });

        // 3. Merge both results and get the assembly name
        var allTypesWithAssembly = context.CompilationProvider
            .Combine(referencedTypes.Combine(sourceClasses.Collect()))
            .Select(static (data, _) =>
            {
                var (compilation, (fromRefs, fromSource)) = data;
                var set = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var t in fromRefs)
                {
                    set.Add(t);
                }
                foreach (var t in Enumerable.OfType<INamedTypeSymbol>(fromSource))
                {
                    set.Add(t);
                }
                var assemblyName = compilation.AssemblyName ?? "Unknown";
                return (AssemblyName: assemblyName, Types: set.ToImmutableArray());
            });

        // 4. Generate code
        context.RegisterSourceOutput(allTypesWithAssembly, static (spc, data) =>
        {
            if (data.Types.IsEmpty)
            {
                return;
            }

            var source = GenerateAddAllHandlersExtension(data.AssemblyName, data.Types);
            spc.AddSource("HandlerRegistrationExtensions.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static void CollectHandlerTypes(INamespaceSymbol ns, ImmutableArray<INamedTypeSymbol>.Builder builder)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol childNs:
                    CollectHandlerTypes(childNs, builder);
                    break;
                case INamedTypeSymbol { IsAbstract: false, IsGenericType: false } type when IsHandlerType(type):
                    builder.Add(type);
                    break;
            }
        }
    }

    private static bool IsHandlerType(INamedTypeSymbol symbol)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            if (!iface.IsGenericType)
            {
                continue;
            }
            var display = iface.OriginalDefinition.ToDisplayString();
            if (display == "NOF.Application.ICommandHandler<TCommand>" ||
                display == "NOF.Application.IEventHandler<TEvent>" ||
                display == "NOF.Application.INotificationHandler<TNotification>" ||
                display == "NOF.Application.IRequestHandler<TRequest>" ||
                display == "NOF.Application.IRequestHandler<TRequest, TResponse>")
            {
                return true;
            }
        }
        return false;
    }

    private static string GenerateAddAllHandlersExtension(string assemblyName,
        ImmutableArray<INamedTypeSymbol> handlerClasses)
    {
        var sanitizedName = assemblyName.Replace(".", "");

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS8620");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using NOF.Infrastructure.Abstraction;");
        sb.AppendLine();

        sb.AppendLine($"namespace {assemblyName}");
        sb.AppendLine("{");

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Auto-generated handler registration extension methods (assembly: {assemblyName}).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static partial class {sanitizedName}Extensions");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine(
            $"        /// Registers all handler types discovered in {assemblyName} and its prefix-matching referenced assemblies");
        sb.AppendLine("        /// into typed HandlerInfos singletons in DI. Keyed service registrations and endpoint name resolution");
        sb.AppendLine(
            "        /// are handled at runtime by <c>AddHandlerInfo</c>.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"services\">The service collection.</param>");
        sb.AppendLine(
            "        /// <returns>A <see cref=\"HandlerSelector\"/> for further endpoint name customization.</returns>");
        sb.AppendLine(
            "        public static global::NOF.Infrastructure.Abstraction.HandlerSelector AddAllHandlers(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");

        var typeFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);

        // Collect typed handler info entries
        var commandInfos = new List<string>();
        var eventInfos = new List<string>();
        var notificationInfos = new List<string>();
        var requestWithoutResponseInfos = new List<string>();
        var requestWithResponseInfos = new List<string>();

        foreach (var handlerClass in handlerClasses)
        {
            CollectHandlerRegistrations(commandInfos, eventInfos, notificationInfos,
                requestWithoutResponseInfos, requestWithResponseInfos,
                handlerClass, typeFormat);
        }

        // AddHandlerInfo handles keyed service registration, endpoint name map, etc. at runtime
        EmitAddInfoCalls(sb, "AddHandlerInfo", commandInfos);
        EmitAddInfoCalls(sb, "AddHandlerInfo", eventInfos);
        EmitAddInfoCalls(sb, "AddHandlerInfo", notificationInfos);
        EmitAddInfoCalls(sb, "AddHandlerInfo", requestWithoutResponseInfos);
        EmitAddInfoCalls(sb, "AddHandlerInfo", requestWithResponseInfos);

        sb.AppendLine("            return new global::NOF.Infrastructure.Abstraction.HandlerSelector(services);");

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void EmitAddInfoCalls(StringBuilder sb, string methodName, List<string> infos)
    {
        if (infos.Count == 0)
        {
            return;
        }

        sb.AppendLine($"            services.{methodName}(");
        for (var i = 0; i < infos.Count; i++)
        {
            var comma = i < infos.Count - 1 ? "," : ");";
            sb.AppendLine($"                {infos[i]}{comma}");
        }
    }

    private static void CollectHandlerRegistrations(
        List<string> commandInfos,
        List<string> eventInfos,
        List<string> notificationInfos,
        List<string> requestWithoutResponseInfos,
        List<string> requestWithResponseInfos,
        INamedTypeSymbol handlerClass,
        SymbolDisplayFormat typeFormat)
    {
        var handlerTypeName = handlerClass.ToDisplayString(typeFormat);

        foreach (var iface in handlerClass.AllInterfaces)
        {
            if (!iface.IsGenericType)
            {
                continue;
            }

            var display = iface.OriginalDefinition.ToDisplayString();

            if (display == "NOF.Application.ICommandHandler<TCommand>")
            {
                var messageType = iface.TypeArguments[0].ToDisplayString(typeFormat);
                commandInfos.Add($"new global::NOF.Infrastructure.Abstraction.CommandHandlerInfo(typeof({handlerTypeName}), typeof({messageType}))");
            }
            else if (display == "NOF.Application.IEventHandler<TEvent>")
            {
                var messageType = iface.TypeArguments[0].ToDisplayString(typeFormat);
                eventInfos.Add($"new global::NOF.Infrastructure.Abstraction.EventHandlerInfo(typeof({handlerTypeName}), typeof({messageType}))");
            }
            else if (display == "NOF.Application.INotificationHandler<TNotification>")
            {
                var messageType = iface.TypeArguments[0].ToDisplayString(typeFormat);
                notificationInfos.Add($"new global::NOF.Infrastructure.Abstraction.NotificationHandlerInfo(typeof({handlerTypeName}), typeof({messageType}))");
            }
            else if (display == "NOF.Application.IRequestHandler<TRequest, TResponse>")
            {
                var messageType = iface.TypeArguments[0].ToDisplayString(typeFormat);
                var responseType = iface.TypeArguments[1].ToDisplayString(typeFormat);
                requestWithResponseInfos.Add($"new global::NOF.Infrastructure.Abstraction.RequestWithResponseHandlerInfo(typeof({handlerTypeName}), typeof({messageType}), typeof({responseType}))");
            }
            else if (display == "NOF.Application.IRequestHandler<TRequest>")
            {
                var messageType = iface.TypeArguments[0].ToDisplayString(typeFormat);
                requestWithoutResponseInfos.Add($"new global::NOF.Infrastructure.Abstraction.RequestWithoutResponseHandlerInfo(typeof({handlerTypeName}), typeof({messageType}))");
            }
        }
    }

}
