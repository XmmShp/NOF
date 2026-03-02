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
        sb.AppendLine("        /// into the HandlerInfos and EndpointNameOptions singletons in DI.");
        sb.AppendLine(
            "        /// Endpoint names are resolved at compile time from <c>[EndpointName]</c> attributes or type names.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"services\">The service collection.</param>");
        sb.AppendLine(
            "        /// <returns>A <see cref=\"HandlerSelector\"/> for further endpoint name customization.</returns>");
        sb.AppendLine(
            "        public static global::NOF.Infrastructure.Abstraction.HandlerSelector AddAllHandlers(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");

        var typeFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);

        // Collect handler info entries and endpoint name mappings
        var entries = new List<string>();
        var endpointNames = new HashSet<(string TypeExpr, string EndpointName)>();

        foreach (var handlerClass in handlerClasses)
        {
            CollectHandlerRegistrations(entries, endpointNames, handlerClass, typeFormat);
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

        // Register event handlers as keyed scoped services in the root container (keyed by composite EventHandlerKey)
        var eventRegistrations = new HashSet<(string HandlerType, string EventType)>();
        foreach (var handlerClass in handlerClasses)
        {
            foreach (var iface in handlerClass.AllInterfaces)
            {
                if (!iface.IsGenericType)
                {
                    continue;
                }

                if (iface.OriginalDefinition.ToDisplayString() == "NOF.Application.IEventHandler<TEvent>")
                {
                    var handlerTypeName = handlerClass.ToDisplayString(typeFormat);
                    var eventTypeName = iface.TypeArguments[0].ToDisplayString(typeFormat);
                    eventRegistrations.Add((handlerTypeName, eventTypeName));
                }
            }
        }

        if (eventRegistrations.Count > 0)
        {
            foreach (var (handlerTypeName, eventTypeName) in eventRegistrations)
            {
                sb.AppendLine($"            services.AddKeyedScoped<global::NOF.Application.IEventHandler, {handlerTypeName}>(global::NOF.Infrastructure.Abstraction.EventHandlerKey.Of(typeof({eventTypeName})));");
            }
        }

        // Populate EndpointNameOptions
        sb.AppendLine("            services.Configure<global::NOF.Infrastructure.Abstraction.EndpointNameOptions>(endpointNameOptions => {");
        foreach (var (typeExpr, name) in endpointNames)
        {
            sb.AppendLine($"                endpointNameOptions.TrySet(typeof({typeExpr}), \"{EscapeString(name)}\");");
        }
        sb.AppendLine("            });");

        sb.AppendLine("            return new global::NOF.Infrastructure.Abstraction.HandlerSelector(services);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static readonly HashSet<string> HandlerInterfaceNames =
    new HashSet<string>
    {
        "NOF.Application.ICommandHandler<TCommand>",
        "NOF.Application.IRequestHandler<TRequest>",
        "NOF.Application.IRequestHandler<TRequest, TResponse>"
    };

    private static void CollectHandlerRegistrations(
        List<string> entries,
        HashSet<(string TypeExpr, string EndpointName)> endpointNames,
        INamedTypeSymbol handlerClass,
        SymbolDisplayFormat typeFormat)
    {
        var handlerTypeName = handlerClass.ToDisplayString(typeFormat);
        var hasNonEventHandler = false;

        foreach (var iface in handlerClass.AllInterfaces)
        {
            if (!iface.IsGenericType)
            {
                continue;
            }

            var display = iface.OriginalDefinition.ToDisplayString();

            if (display == "NOF.Application.ICommandHandler<TCommand>")
            {
                var messageSymbol = (INamedTypeSymbol)iface.TypeArguments[0];
                var messageType = messageSymbol.ToDisplayString(typeFormat);
                entries.Add($"new global::NOF.Infrastructure.Abstraction.HandlerInfo(global::NOF.Infrastructure.Abstraction.HandlerKind.Command, typeof({handlerTypeName}), typeof({messageType}), null)");
                endpointNames.Add((messageType, ResolveEndpointNameForLeafType(messageSymbol)));
                hasNonEventHandler = true;
            }
            else if (display == "NOF.Application.IEventHandler<TEvent>")
            {
                var messageSymbol = (INamedTypeSymbol)iface.TypeArguments[0];
                var messageType = messageSymbol.ToDisplayString(typeFormat);
                entries.Add($"new global::NOF.Infrastructure.Abstraction.HandlerInfo(global::NOF.Infrastructure.Abstraction.HandlerKind.Event, typeof({handlerTypeName}), typeof({messageType}), null)");
            }
            else if (display == "NOF.Application.INotificationHandler<TNotification>")
            {
                var messageSymbol = (INamedTypeSymbol)iface.TypeArguments[0];
                var messageType = messageSymbol.ToDisplayString(typeFormat);
                entries.Add($"new global::NOF.Infrastructure.Abstraction.HandlerInfo(global::NOF.Infrastructure.Abstraction.HandlerKind.Notification, typeof({handlerTypeName}), typeof({messageType}), null)");
                hasNonEventHandler = true;
            }
            else if (display == "NOF.Application.IRequestHandler<TRequest, TResponse>")
            {
                var messageSymbol = (INamedTypeSymbol)iface.TypeArguments[0];
                var messageType = messageSymbol.ToDisplayString(typeFormat);
                var responseType = iface.TypeArguments[1].ToDisplayString(typeFormat);
                entries.Add($"new global::NOF.Infrastructure.Abstraction.HandlerInfo(global::NOF.Infrastructure.Abstraction.HandlerKind.RequestWithResponse, typeof({handlerTypeName}), typeof({messageType}), typeof({responseType}))");
                endpointNames.Add((messageType, ResolveEndpointNameForLeafType(messageSymbol)));
                hasNonEventHandler = true;
            }
            else if (display == "NOF.Application.IRequestHandler<TRequest>")
            {
                var messageSymbol = (INamedTypeSymbol)iface.TypeArguments[0];
                var messageType = messageSymbol.ToDisplayString(typeFormat);
                entries.Add($"new global::NOF.Infrastructure.Abstraction.HandlerInfo(global::NOF.Infrastructure.Abstraction.HandlerKind.RequestWithoutResponse, typeof({handlerTypeName}), typeof({messageType}), null)");
                endpointNames.Add((messageType, ResolveEndpointNameForLeafType(messageSymbol)));
                hasNonEventHandler = true;
            }
        }

        // Only register endpoint name for the handler type itself when it has non-event handler interfaces.
        // Event-only handlers do not need endpoint names.
        if (hasNonEventHandler)
        {
            endpointNames.Add((handlerTypeName, ResolveEndpointNameForHandlerType(handlerClass)));
        }
    }

    /// <summary>
    /// Resolves the endpoint name for a handler type at compile time.
    /// Mirrors the original reflection-based GetEndpointName logic:
    /// 1. Check [EndpointName] attribute on the handler type
    /// 2. Find ICommandHandler/IRequestHandler interfaces → use message type's endpoint name
    /// 3. Fallback to BuildSafeTypeName
    /// </summary>
    private static string ResolveEndpointNameForHandlerType(INamedTypeSymbol handlerSymbol)
    {
        // 1. Check for [EndpointName("...")] attribute
        var attrName = GetEndpointNameAttribute(handlerSymbol);
        if (attrName != null)
        {
            return attrName;
        }

        // 2. Filter to only ICommandHandler<>/IRequestHandler<>/IRequestHandler<,> message types
        //    (mirrors the original code which only looks at these three, NOT IEventHandler/INotificationHandler)
        var handlerMessageTypes = new List<INamedTypeSymbol>();
        foreach (var iface in handlerSymbol.AllInterfaces)
        {
            if (!iface.IsGenericType)
            {
                continue;
            }
            var display = iface.OriginalDefinition.ToDisplayString();
            if (HandlerInterfaceNames.Contains(display))
            {
                handlerMessageTypes.Add((INamedTypeSymbol)iface.TypeArguments[0]);
            }
        }

        if (handlerMessageTypes.Count == 1)
        {
            return ResolveEndpointNameForLeafType(handlerMessageTypes[0]);
        }

        // 3. Fallback
        return BuildSafeTypeName(handlerSymbol);
    }

    /// <summary>
    /// Resolves the endpoint name for a leaf type (message type, not a handler type).
    /// Checks [EndpointName] attribute first, then falls back to BuildSafeTypeName.
    /// </summary>
    private static string ResolveEndpointNameForLeafType(INamedTypeSymbol symbol)
    {
        return GetEndpointNameAttribute(symbol) ?? BuildSafeTypeName(symbol);
    }

    /// <summary>
    /// Returns the [EndpointName("...")] value if present, or null.
    /// </summary>
    private static string? GetEndpointNameAttribute(INamedTypeSymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == "NOF.Contract.EndpointNameAttribute" &&
                attr.ConstructorArguments.Length > 0 &&
                attr.ConstructorArguments[0].Value is string name)
            {
                return name;
            }
        }
        return null;
    }

    /// <summary>
    /// Compile-time equivalent of EndpointNameProvider.BuildSafeTypeName.
    /// Produces a stable, safe string from the fully qualified type name.
    /// </summary>
    private static string BuildSafeTypeName(INamedTypeSymbol symbol)
    {
        if (!symbol.IsGenericType)
        {
            var fullName = GetFullMetadataName(symbol);
            return fullName.Replace('.', '_').Replace("+", "____");
        }

        var originalDef = symbol.OriginalDefinition;
        var defName = GetFullMetadataName(originalDef);
        // Remove the arity suffix (e.g., `2)
        var backtickIndex = defName.LastIndexOf('`');
        if (backtickIndex >= 0)
        {
            defName = defName.Substring(0, backtickIndex);
        }
        defName = defName.Replace('.', '_').Replace("+", "____");

        var args = symbol.TypeArguments
            .Select(a => a is INamedTypeSymbol nts ? BuildSafeTypeName(nts) : a.Name)
            .ToArray();

        return $"{defName}__{string.Join("___", args)}";
    }

    /// <summary>
    /// Gets the fully qualified metadata name (namespace + nested types) for a symbol.
    /// </summary>
    private static string GetFullMetadataName(ISymbol symbol)
    {
        if (symbol.ContainingType != null)
        {
            return GetFullMetadataName(symbol.ContainingType) + "+" + symbol.MetadataName;
        }
        if (symbol.ContainingNamespace != null && !symbol.ContainingNamespace.IsGlobalNamespace)
        {
            return GetFullMetadataName(symbol.ContainingNamespace) + "." + symbol.MetadataName;
        }
        return symbol.MetadataName;
    }

    private static string EscapeString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

}
