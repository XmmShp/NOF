using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF.Domain.SourceGenerator;

/// <summary>
/// Source generator: Detects classes marked with FailureAttribute and generates static error instances
/// </summary>
[Generator]
public class FailureGenerator : IIncrementalGenerator
{
    // Diagnostic descriptors for error reporting
    private static readonly DiagnosticDescriptor DuplicateFailureNameDescriptor = new(
        "NOF001",
        "Duplicate Failure Name",
        "Class '{0}' contains duplicate Failure names: {1}",
        "FailureGenerator",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateFailureCodeDescriptor = new(
        "NOF002",
        "Duplicate Failure Code",
        "Class '{0}' contains duplicate Failure codes: {1}",
        "FailureGenerator",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Get all classes with FailureAttribute
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        // Register source output
        context.RegisterSourceOutput(classDeclarations.Collect(), static (spc, source) => Execute(source, spc));
    }

    /// <summary>
    /// Determines if a syntax node is a generation target (class or record declaration with attributes)
    /// </summary>
    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
    {
        return (node is (ClassDeclarationSyntax or RecordDeclarationSyntax) and TypeDeclarationSyntax { AttributeLists.Count: > 0 });
    }

    /// <summary>
    /// Gets the semantic model target
    /// </summary>
    private static FailureClassInfo? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;

        // Get the type's semantic symbol
        var typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
        if (typeSymbol is null)
        {
            return null;
        }

        // Check if it's a partial type
        if (typeDeclaration.Modifiers.All(m => m.Text != "partial"))
        {
            return null;
        }

        // Get FailureAttribute from this specific syntax declaration only
        var errorAttributes = new List<AttributeData>();
        foreach (var attributeList in typeDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var attributeSymbol = context.SemanticModel.GetSymbolInfo(attribute).Symbol?.ContainingType;
                if (attributeSymbol?.ToDisplayString() == "NOF.Domain.FailureAttribute")
                {
                    var attributeData = context.SemanticModel.GetSymbolInfo(attribute).Symbol as IMethodSymbol;
                    if (attributeData?.ContainingType != null)
                    {
                        // Get the attribute data from the semantic model
                        var semanticAttribute = typeSymbol.GetAttributes()
                            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "NOF.Domain.FailureAttribute" &&
                                                a.ApplicationSyntaxReference?.GetSyntax() == attribute);
                        if (semanticAttribute != null)
                        {
                            errorAttributes.Add(semanticAttribute);
                        }
                    }
                }
            }
        }

        if (errorAttributes.Count == 0)
        {
            return null;
        }

        var errors = (errorAttributes.Where(attr => attr.ConstructorArguments.Length == 3)
            .Select(attr => new { attr, name = attr.ConstructorArguments[0].Value?.ToString() ?? string.Empty })
            .Select(t => new { t, message = t.attr.ConstructorArguments[1].Value?.ToString() ?? string.Empty })
            .Select(t => new { t, errorCode = (int)(t.t.attr.ConstructorArguments[2].Value ?? 0) })
            .Where(t => !string.IsNullOrEmpty(t.t.t.name))
            .Select(t => new FailureInfo { Name = t.t.t.name, Message = t.t.message, FailureCode = t.errorCode })).ToList();

        if (errors.Count == 0)
        {
            return null;
        }

        return new FailureClassInfo
        {
            TypeName = typeSymbol.Name,
            Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
            IsRecord = typeDeclaration is RecordDeclarationSyntax,
            IsAbstract = typeSymbol.IsAbstract,
            Failures = errors,
            Location = typeDeclaration.GetLocation()
        };
    }

    /// <summary>
    /// Execute code generation
    /// </summary>
    private static void Execute(ImmutableArray<FailureClassInfo?> errorClasses, SourceProductionContext context)
    {
        if (errorClasses.IsDefaultOrEmpty)
        {
            return;
        }

        var validClasses = errorClasses
            .Where(c => c is not null)
            .ToList();

        if (validClasses.Count == 0)
        {
            return;
        }

        // Group by namespace and type name, merge all Failure info for the same class
        var groupedClasses = validClasses
            .GroupBy(c => new { c!.Namespace, c.TypeName })
            .Select(g => new FailureClassInfo
            {
                TypeName = g.Key.TypeName,
                Namespace = g.Key.Namespace,
                IsRecord = g.First()!.IsRecord,
                IsAbstract = g.First()!.IsAbstract,
                Failures = g.SelectMany(c => c!.Failures).ToList(),
                Location = g.First()!.Location
            })
            .ToList();

        // Check for duplicate Name or ErrorCode in each merged class
        foreach (var errorClass in groupedClasses)
        {
            // Check for duplicate Name
            var duplicateNames = errorClass.Failures.GroupBy(e => e.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateNames.Count > 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateFailureNameDescriptor,
                    errorClass.Location, errorClass.TypeName, string.Join(", ", duplicateNames)));
                continue;
            }

            // Check for duplicate ErrorCode
            var duplicateErrorCodes = errorClass.Failures.GroupBy(e => e.FailureCode).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateErrorCodes.Count > 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateFailureCodeDescriptor,
                    errorClass.Location, errorClass.TypeName, string.Join(", ", duplicateErrorCodes)));
                continue;
            }

            // Only generate code if there are no duplicates
            var source = GenerateFailureClass(errorClass);
            // Use namespace and type name to generate file name
            var safeFileName = $"{errorClass.Namespace.Replace('.', '_')}_{errorClass.TypeName}.g.cs";
            context.AddSource(safeFileName, SourceText.From(source, Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generate partial class code for error class
    /// </summary>
    private static string GenerateFailureClass(FailureClassInfo errorClass)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {errorClass.Namespace}");
        sb.AppendLine("{");

        // Generate class or record declaration
        var typeKeyword = errorClass.IsRecord ? "record" : "class";
        var abstractKeyword = errorClass.IsAbstract ? "abstract " : "";

        sb.AppendLine($"    public {abstractKeyword}partial {typeKeyword} {errorClass.TypeName}");
        sb.AppendLine("    {");

        // Generate static error instances
        foreach (var error in errorClass.Failures)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// {error.Message}");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        public static readonly NOF.Failure {error.Name} = new(\"{error.Message}\", {error.FailureCode});");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Error class information
    /// </summary>
    private class FailureClassInfo
    {
        public string TypeName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public bool IsRecord { get; set; }
        public bool IsAbstract { get; set; }
        public List<FailureInfo> Failures { get; set; } = [];
        public Location Location { get; set; } = Location.None;
    }

    /// <summary>
    /// Single error information
    /// </summary>
    private class FailureInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int FailureCode { get; set; }
    }
}
