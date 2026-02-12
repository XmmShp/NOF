using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
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

    private static string GenerateAddAllHandlersExtension(string assemblyName, ImmutableArray<INamedTypeSymbol> handlerClasses)
    {
        var sanitizedName = assemblyName.Replace(".", "");

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS8620");
        sb.AppendLine();
        sb.AppendLine("using NOF.Infrastructure.Core;");
        sb.AppendLine();

        sb.AppendLine($"namespace {assemblyName}");
        sb.AppendLine("{");

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Auto-generated handler registration extension methods (assembly: {assemblyName}).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static partial class {sanitizedName}Extensions");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Registers all handler types discovered in {assemblyName} and its prefix-matching referenced assemblies");
        sb.AppendLine("        /// into the HandlerInfos singleton in DI.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"services\">The service collection.</param>");
        sb.AppendLine("        /// <returns>The service collection for chaining.</returns>");
        sb.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddAllHandlers(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");

        var typeFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);

        // Collect all handler info entries
        var entries = new List<string>();
        foreach (var handlerClass in handlerClasses)
        {
            CollectHandlerRegistrations(entries, handlerClass, typeFormat);
        }

        if (entries.Count > 0)
        {
            sb.AppendLine("            services.AddHandlerInfo(");
            for (var i = 0; i < entries.Count; i++)
            {
                var comma = i < entries.Count - 1 ? "," : ");";
                sb.AppendLine($"                {entries[i]}{comma}");
            }
        }

        sb.AppendLine("            return services;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void CollectHandlerRegistrations(List<string> entries, INamedTypeSymbol handlerClass, SymbolDisplayFormat typeFormat)
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
                entries.Add($"new global::NOF.Infrastructure.Core.HandlerInfo(global::NOF.Infrastructure.Core.HandlerKind.Command, typeof({handlerTypeName}), typeof({messageType}), null)");
            }
            else if (display == "NOF.Application.IEventHandler<TEvent>")
            {
                var messageType = iface.TypeArguments[0].ToDisplayString(typeFormat);
                entries.Add($"new global::NOF.Infrastructure.Core.HandlerInfo(global::NOF.Infrastructure.Core.HandlerKind.Event, typeof({handlerTypeName}), typeof({messageType}), null)");
            }
            else if (display == "NOF.Application.INotificationHandler<TNotification>")
            {
                var messageType = iface.TypeArguments[0].ToDisplayString(typeFormat);
                entries.Add($"new global::NOF.Infrastructure.Core.HandlerInfo(global::NOF.Infrastructure.Core.HandlerKind.Notification, typeof({handlerTypeName}), typeof({messageType}), null)");
            }
            else if (display == "NOF.Application.IRequestHandler<TRequest, TResponse>")
            {
                var messageType = iface.TypeArguments[0].ToDisplayString(typeFormat);
                var responseType = iface.TypeArguments[1].ToDisplayString(typeFormat);
                entries.Add($"new global::NOF.Infrastructure.Core.HandlerInfo(global::NOF.Infrastructure.Core.HandlerKind.RequestWithResponse, typeof({handlerTypeName}), typeof({messageType}), typeof({responseType}))");
            }
            else if (display == "NOF.Application.IRequestHandler<TRequest>")
            {
                var messageType = iface.TypeArguments[0].ToDisplayString(typeFormat);
                entries.Add($"new global::NOF.Infrastructure.Core.HandlerInfo(global::NOF.Infrastructure.Core.HandlerKind.RequestWithoutResponse, typeof({handlerTypeName}), typeof({messageType}), null)");
            }
        }
    }
}
