using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF.Application.SourceGenerator;

[Generator]
public sealed class SplitInterfaceAutoInjectGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var sourceClasses = context.SyntaxProvider
            .CreateSyntaxProvider<ImmutableArray<RegistrationInfo>>(
                static (node, _) => node is ClassDeclarationSyntax { BaseList.Types.Count: > 0 },
                static (ctx, _) =>
                {
                    if (ctx.Node is not ClassDeclarationSyntax classDeclaration
                        || ctx.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol
                        || classSymbol.IsAbstract)
                    {
                        return [];
                    }

                    var registrations = CollectRegistrations(classDeclaration, classSymbol, ctx.SemanticModel);
                    return registrations.Count == 0 ? [] : [.. registrations];
                })
            .Where(static registrations => !registrations.IsDefaultOrEmpty);

        var allTypesWithAssembly = context.CompilationProvider
            .Combine(sourceClasses.Collect())
            .Select((data, _) =>
            {
                var (compilation, registrationGroups) = data;
                var assemblyName = compilation.AssemblyName ?? "Unknown";
                var registrations = new List<RegistrationInfo>();
                var seen = new HashSet<string>(System.StringComparer.Ordinal);

                foreach (var registration in registrationGroups.SelectMany(x => x))
                {
                    if (!seen.Add(registration.ServiceType + "|" + registration.ImplementationType))
                    {
                        continue;
                    }

                    registrations.Add(registration);
                }

                return new RegistrationCollection(assemblyName, registrations.ToImmutableArray());
            });

        context.RegisterSourceOutput(allTypesWithAssembly, (spc, data) =>
        {
            if (data.Registrations.IsDefaultOrEmpty)
            {
                return;
            }

            var source = GenerateInitializer(data.AssemblyName, data.Registrations);
            spc.AddSource("SplitInterfaceAutoInjectAssemblyInitializer.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static List<RegistrationInfo> CollectRegistrations(
        ClassDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SemanticModel semanticModel)
    {
        var registrations = new List<RegistrationInfo>();
        var implementationType = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (classDeclaration.BaseList is null)
        {
            return registrations;
        }

        foreach (var baseType in classDeclaration.BaseList.Types)
        {
            if (!TryGetSplitOperationInterface(baseType.Type, semanticModel, out var serviceType))
            {
                continue;
            }

            registrations.Add(new RegistrationInfo(serviceType, implementationType));
        }

        return registrations;
    }

    private static bool TryGetSplitOperationInterface(
        TypeSyntax typeSyntax,
        SemanticModel semanticModel,
        out string serviceType)
    {
        serviceType = string.Empty;

        if (typeSyntax is not QualifiedNameSyntax qualifiedName)
        {
            return false;
        }

        var operationName = qualifiedName.Right.Identifier.ValueText;
        if (string.IsNullOrWhiteSpace(operationName))
        {
            return false;
        }

        var containingType = ResolveContainingType(qualifiedName.Left, semanticModel);
        if (containingType is null || !SplitInterfaceSymbolHelper.ImplementsSplitedInterface(containingType))
        {
            return false;
        }

        serviceType = $"{containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{operationName}";
        return true;
    }

    private static INamedTypeSymbol? ResolveContainingType(NameSyntax nameSyntax, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetSymbolInfo(nameSyntax).Symbol;
        if (symbol is INamedTypeSymbol namedType)
        {
            return namedType;
        }

        return semanticModel.GetTypeInfo(nameSyntax).Type as INamedTypeSymbol;
    }

    private static string GenerateInitializer(string assemblyName, ImmutableArray<RegistrationInfo> registrations)
    {
        var sanitizedName = assemblyName.Replace(".", "");
        var initializerTypeName = $"__{sanitizedName}SplitInterfaceAutoInjectAssemblyInitializer";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"[assembly: global::NOF.Annotation.AssemblyInitializeAttribute<global::{assemblyName}.{initializerTypeName}>]");
        sb.AppendLine();
        sb.AppendLine($"namespace {assemblyName}");
        sb.AppendLine("{");
        sb.AppendLine($"    internal sealed class {initializerTypeName} : global::NOF.Annotation.IAssemblyInitializer");
        sb.AppendLine("    {");
        sb.AppendLine("        private static int _initialized;");
        sb.AppendLine();
        sb.AppendLine("        public static void Initialize()");
        sb.AppendLine("        {");
        sb.AppendLine("            if (global::System.Threading.Interlocked.Exchange(ref _initialized, 1) == 1)");
        sb.AppendLine("            {");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();

        foreach (var registration in registrations)
        {
            sb.AppendLine($"            global::NOF.Annotation.AutoInjectRegistry.Register(typeof({registration.ServiceType}), typeof({registration.ImplementationType}), global::NOF.Annotation.Lifetime.Transient, useFactory: false);");
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private sealed class RegistrationCollection
    {
        public RegistrationCollection(string assemblyName, ImmutableArray<RegistrationInfo> registrations)
        {
            AssemblyName = assemblyName;
            Registrations = registrations;
        }

        public string AssemblyName { get; }
        public ImmutableArray<RegistrationInfo> Registrations { get; }
    }

    private sealed class RegistrationInfo
    {
        public RegistrationInfo(string serviceType, string implementationType)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
        }

        public string ServiceType { get; }
        public string ImplementationType { get; }
    }
}
