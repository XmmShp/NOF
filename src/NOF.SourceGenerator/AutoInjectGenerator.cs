using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF;

/// <summary>
/// Source generator: detects service classes marked with AutoInjectAttribute (including referenced projects) and generates DI registration code.
/// </summary>
[Generator]
public class AutoInjectGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Extract classes with AutoInject from current project source code (syntax + semantic)
        var sourceClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, _) =>
                {
                    if (ctx.Node is not ClassDeclarationSyntax cds)
                    {
                        return null;
                    }
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(cds);
                    if (symbol is { IsAbstract: false, DeclaredAccessibility: Accessibility.Public } &&
                        HasAutoInjectAttribute(symbol))
                    {
                        return symbol;
                    }
                    return null;
                })
            .Where(static s => s is not null);

        // 2. Extract public non-abstract classes with AutoInject from all referenced assemblies (metadata)
        var referencedTypes = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
                foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
                {
                    CollectAutoInjectTypes(assembly.GlobalNamespace, builder);
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

            var source = GenerateServiceRegistrationExtension(data.AssemblyName, data.Types);
            spc.AddSource("ServiceCollectionExtensions.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static void CollectAutoInjectTypes(INamespaceSymbol ns, ImmutableArray<INamedTypeSymbol>.Builder builder)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol childNs:
                    CollectAutoInjectTypes(childNs, builder);
                    break;
                case INamedTypeSymbol { IsAbstract: false, DeclaredAccessibility: Accessibility.Public } type when HasAutoInjectAttribute(type):
                    builder.Add(type);
                    break;
            }
        }
    }

    private static bool HasAutoInjectAttribute(ISymbol symbol)
        => symbol.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == "NOF.AutoInjectAttribute");

    /// <summary>
    /// Converts an assembly name to a safe method name (replaces unsafe characters with underscores).
    /// </summary>
    private static string SanitizeAssemblyName(string assemblyName)
    {
        var sb = new StringBuilder();
        foreach (var c in assemblyName)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('_');
            }
        }
        return sb.ToString();
    }

    private static string GenerateServiceRegistrationExtension(string assemblyName, ImmutableArray<INamedTypeSymbol> serviceClasses)
    {
        const string targetNamespace = "NOF.Generated";
        var sanitizedName = SanitizeAssemblyName(assemblyName);
        var methodName = $"Add{sanitizedName}AutoInjectServices";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using System;");
        sb.AppendLine();

        sb.AppendLine($"namespace {targetNamespace}");
        sb.AppendLine("{");

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Auto-generated service registration extension methods (assembly: {assemblyName}).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static class __ServiceCollectionExtensions__");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Auto-registers all services marked with AutoInjectAttribute in {assemblyName}.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"services\">The service collection.</param>");
        sb.AppendLine("        /// <returns>The service collection for chaining.</returns>");

        sb.AppendLine($"        public static IServiceCollection {methodName}(this IServiceCollection services)");
        sb.AppendLine("        {");

        foreach (var serviceClass in serviceClasses)
        {
            GenerateServiceRegistration(sb, serviceClass);
        }

        sb.AppendLine("            return services;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateServiceRegistration(StringBuilder sb, INamedTypeSymbol serviceClass)
    {
        var attribute = serviceClass.GetAttributes()
            .FirstOrDefault(attr
                => attr.AttributeClass is not null
                   && attr.AttributeClass.ToDisplayString().Equals("NOF.AutoInjectAttribute"));

        if (attribute is null)
        {
            return;
        }

        // Parse lifetime
        if (attribute.ConstructorArguments.Length <= 0 || attribute.ConstructorArguments[0].Value is not int intValue)
        {
            return;
        }

        var lifetime = intValue switch
        {
            0 => Lifetime.Singleton,
            1 => Lifetime.Scoped,
            2 => Lifetime.Transient,
            _ => throw new ArgumentOutOfRangeException()
        };

        // Get fully qualified type name without global:: prefix
        var typeFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
            .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        var serviceTypeName = serviceClass.ToDisplayString(typeFormat);

        // Check if there are explicit RegisterTypes
        var registerTypesArg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "RegisterTypes").Value;
        var hasExplicitRegisterTypes = !registerTypesArg.IsNull;

        var typesToRegister = new List<string>();

        if (hasExplicitRegisterTypes)
        {
            // RegisterTypes is Type[], each value is ITypeSymbol
            foreach (var typedConstant in registerTypesArg.Values)
            {
                if (typedConstant.Value is ITypeSymbol regType && regType.ToDisplayString() != serviceTypeName)
                {
                    typesToRegister.Add(regType.ToDisplayString(typeFormat));
                }
            }
        }
        else
        {
            // Default: register all implemented interfaces
            typesToRegister.AddRange(serviceClass.AllInterfaces.Select(i => i.ToDisplayString(typeFormat)));
        }

        // Handle Singleton / Scoped (shared instance needed)
        if (lifetime == Lifetime.Singleton || lifetime == Lifetime.Scoped)
        {
            if (typesToRegister.Count == 1)
            {
                sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof({typesToRegister[0]}), typeof({serviceTypeName}), {ToLifeTime(lifetime)}));");
            }
            else
            {
                // Register self first
                sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof({serviceTypeName}), typeof({serviceTypeName}), {ToLifeTime(lifetime)}));");
                // Then register interfaces (factory delegate pointing to self)
                foreach (var typeName in typesToRegister)
                {
                    sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof({typeName}), sp => sp.GetRequiredService<{serviceTypeName}>(), {ToLifeTime(lifetime)}));");
                }
            }
        }
        else // Transient
        {
            if (hasExplicitRegisterTypes)
            {
                foreach (var typeName in registerTypesArg.Values
                             .Select(v => v.Value as ITypeSymbol)
                             .Where(t => t != null)
                             .Select(t => t!.ToDisplayString(typeFormat)))
                {
                    sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof({typeName}), typeof({serviceTypeName}), {ToLifeTime(lifetime)}));");
                }
            }
            else
            {
                if (typesToRegister.Count > 0)
                {
                    foreach (var typeName in typesToRegister)
                    {
                        sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof({typeName}), typeof({serviceTypeName}), {ToLifeTime(lifetime)}));");
                    }
                }
                else
                {
                    // No interfaces, register self
                    sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof({serviceTypeName}), typeof({serviceTypeName}), {ToLifeTime(lifetime)}));");
                }
            }
        }

        sb.AppendLine();
    }

    private static string ToLifeTime(Lifetime lifetime)
    {
        return lifetime switch
        {
            Lifetime.Singleton => "ServiceLifetime.Singleton",
            Lifetime.Scoped => "ServiceLifetime.Scoped",
            Lifetime.Transient => "ServiceLifetime.Transient",
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null)
        };
    }
}

internal enum Lifetime
{
    Singleton = 0,
    Scoped = 1,
    Transient = 2
}
