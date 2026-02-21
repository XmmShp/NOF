using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace NOF.Domain.SourceGenerator;

/// <summary>
/// Generates value object boilerplate for structs annotated with
/// <c>[ValueObject&lt;TPrimitive&gt;]</c>.
/// </summary>
[Generator]
public class ValueObjectGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "NOF.Domain.ValueObjectAttribute<TPrimitive>";
    private const string NewableAttributeMetadataName = "NOF.Domain.NewableValueObjectAttribute";

    private static readonly DiagnosticDescriptor MustBePartialDescriptor = new(
        id: "NOF010",
        title: "ValueObject struct must be partial",
        messageFormat: "'{0}' is annotated with [ValueObject] but is not declared as partial",
        category: "ValueObjectGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ValidateMustBeStaticDescriptor = new(
        id: "NOF011",
        title: "ValueObject Validate method must be static",
        messageFormat: "'{0}.Validate({1})' must be a static method",
        category: "ValueObjectGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NewableMustBeLongDescriptor = new(
        id: "NOF012",
        title: "[NewableValueObject] requires ValueObject<long>",
        messageFormat: "'{0}' is annotated with [NewableValueObject] but its primitive type is '{1}'. Only ValueObject<long> is supported.",
        category: "ValueObjectGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is StructDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, _) => GetValueObjectInfo(ctx))
            .Where(static x => x is not null);

        context.RegisterSourceOutput(candidates.Collect(), static (spc, items) =>
        {
            foreach (var result in items.Where(x => x is not null).Cast<ValueObjectResult>())
            {
                foreach (var diag in result.Diagnostics)
                {
                    spc.ReportDiagnostic(diag);
                }

                if (result.Info is not null)
                {
                    var source = GenerateSource(result.Info);
                    spc.AddSource($"{result.Info.Namespace.Replace('.', '_')}_{result.Info.TypeName}.g.cs",
                        SourceText.From(source, Encoding.UTF8));
                }
            }
        });
    }

    private static ValueObjectResult? GetValueObjectInfo(GeneratorSyntaxContext ctx)
    {
        var syntax = (StructDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(syntax) as INamedTypeSymbol;
        if (symbol is null)
        {
            return null;
        }

        // Find [ValueObject<TPrimitive>]
        INamedTypeSymbol? primitiveType = null;
        var primitiveNullableAnnotation = NullableAnnotation.None;
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass is null)
            {
                continue;
            }

            if (attr.AttributeClass.OriginalDefinition.ToDisplayString() != AttributeMetadataName)
            {
                continue;
            }

            if (attr.AttributeClass.TypeArguments.Length != 1)
            {
                continue;
            }

            primitiveType = attr.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
            primitiveNullableAnnotation = attr.AttributeClass.TypeArgumentNullableAnnotations[0];
            break;
        }

        if (primitiveType is null)
        {
            return null;
        }

        var result = new ValueObjectResult();

        // Must be partial
        if (syntax.Modifiers.All(m => m.Text != "partial"))
        {
            result.Diagnostics.Add(Diagnostic.Create(MustBePartialDescriptor, syntax.Identifier.GetLocation(), symbol.Name));
            return result; // no code gen
        }

        var typeFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);

        var primitiveName = primitiveType.ToDisplayString(typeFormat);

        // Detect optional static void Validate(TPrimitive value) — emit error if non-static
        var hasValidateMethod = false;
        foreach (var member in symbol.GetMembers("Validate"))
        {
            if (member is not IMethodSymbol method)
            {
                continue;
            }

            if (method.Parameters.Length != 1)
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, primitiveType))
            {
                continue;
            }

            if (!method.IsStatic)
            {
                result.Diagnostics.Add(Diagnostic.Create(
                    ValidateMustBeStaticDescriptor,
                    member.Locations.FirstOrDefault() ?? Location.None,
                    symbol.Name, primitiveName));
                return result; // no code gen
            }

            hasValidateMethod = true;
            break;
        }

        // Non-nullable reference type primitive → emit null-guard in Of()
        // Annotated = string?, NotAnnotated = string, None = no nullable context
        var primitiveRequiresNullCheck = !primitiveType.IsValueType
            && primitiveNullableAnnotation != NullableAnnotation.Annotated;

        // Detect [NewableValueObject] — only valid on ValueObject<long>
        var hasNewableAttribute = symbol.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == NewableAttributeMetadataName);

        if (hasNewableAttribute && primitiveType.SpecialType != SpecialType.System_Int64)
        {
            result.Diagnostics.Add(Diagnostic.Create(
                NewableMustBeLongDescriptor,
                syntax.Identifier.GetLocation(),
                symbol.Name, primitiveName));
            return result; // no code gen
        }

        result.Info = new ValueObjectInfo
        {
            TypeName = symbol.Name,
            Namespace = symbol.ContainingNamespace.ToDisplayString(),
            PrimitiveFullName = primitiveName,
            IsReadonly = syntax.Modifiers.Any(m => m.Text == "readonly"),
            HasValidateMethod = hasValidateMethod,
            PrimitiveIsValueType = primitiveType.IsValueType,
            PrimitiveRequiresNullCheck = primitiveRequiresNullCheck,
            HasNewMethod = hasNewableAttribute,
        };
        return result;
    }

    private static string GenerateSource(ValueObjectInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {info.Namespace}");
        sb.AppendLine("{");

        var readonlyKeyword = info.IsReadonly ? "readonly " : "";

        sb.AppendLine($"    [global::System.Text.Json.Serialization.JsonConverter(typeof({info.TypeName}.__JsonConverter))]");
        sb.AppendLine($"    {readonlyKeyword}partial struct {info.TypeName}");
        sb.AppendLine($"        : global::System.IEquatable<{info.TypeName}>");
        sb.AppendLine("    {");

        // Private backing field
        sb.AppendLine($"        private readonly {info.PrimitiveFullName} _value;");
        sb.AppendLine();

        // Private constructor
        sb.AppendLine($"        private {info.TypeName}({info.PrimitiveFullName} value)");
        sb.AppendLine("        {");
        sb.AppendLine("            _value = value;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Of factory method — only entry point from primitive
        sb.AppendLine($"        public static {info.TypeName} Of({info.PrimitiveFullName} value)");
        sb.AppendLine("        {");
        if (info.PrimitiveRequiresNullCheck)
        {
            sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(value);");
        }

        if (info.HasValidateMethod)
        {
            sb.AppendLine("            Validate(value);");
        }

        sb.AppendLine($"            return new {info.TypeName}(value);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Of(TPrimitive?) overload — only for value type primitives (e.g. int?, long?)
        // Reference types already accept null via Of(T) since T and T? share the same signature
        if (info.PrimitiveIsValueType)
        {
            sb.AppendLine($"        public static {info.TypeName}? Of({info.PrimitiveFullName}? value)");
            sb.AppendLine("            => value.HasValue ? Of(value.Value) : null;");
            sb.AppendLine();
        }

        // Explicit cast: value object → primitive (only direction kept)
        sb.AppendLine($"        public static explicit operator {info.PrimitiveFullName}({info.TypeName} vo)");
        sb.AppendLine("            => vo._value;");
        sb.AppendLine();

        // Equals / GetHashCode / ToString
        sb.AppendLine($"        public bool Equals({info.TypeName} other) => global::System.Collections.Generic.EqualityComparer<{info.PrimitiveFullName}>.Default.Equals(_value, other._value);");
        sb.AppendLine($"        public override bool Equals(object? obj) => obj is {info.TypeName} other && Equals(other);");
        if (info.PrimitiveIsValueType)
        {
            sb.AppendLine($"        public override int GetHashCode() => _value.GetHashCode();");
            sb.AppendLine($"        public override string? ToString() => _value.ToString();");
        }
        else
        {
            sb.AppendLine($"        public override int GetHashCode() => _value?.GetHashCode() ?? 0;");
            sb.AppendLine($"        public override string? ToString() => _value?.ToString();");
        }
        sb.AppendLine();

        // == and !=
        sb.AppendLine($"        public static bool operator ==({info.TypeName} left, {info.TypeName} right) => left.Equals(right);");
        sb.AppendLine($"        public static bool operator !=({info.TypeName} left, {info.TypeName} right) => !left.Equals(right);");
        sb.AppendLine();

        // New() — only when [NewableValueObject] is present
        if (info.HasNewMethod)
        {
            sb.AppendLine($"        public static {info.TypeName} New()");
            sb.AppendLine($"            => Of(global::NOF.Domain.IdGenerator.Current.NextId());");
            sb.AppendLine();
        }

        // Nested JsonConverter
        sb.AppendLine($"        [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.AppendLine($"        public sealed class __JsonConverter : global::System.Text.Json.Serialization.JsonConverter<{info.TypeName}>");
        sb.AppendLine("        {");
        sb.AppendLine($"            public override {info.TypeName} Read(ref global::System.Text.Json.Utf8JsonReader reader, global::System.Type typeToConvert, global::System.Text.Json.JsonSerializerOptions options)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var primitive = global::System.Text.Json.JsonSerializer.Deserialize<{info.PrimitiveFullName}>(ref reader, options)!;");
        sb.AppendLine($"                return {info.TypeName}.Of(primitive);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            public override void Write(global::System.Text.Json.Utf8JsonWriter writer, {info.TypeName} value, global::System.Text.Json.JsonSerializerOptions options)");
        sb.AppendLine("            {");
        sb.AppendLine($"                global::System.Text.Json.JsonSerializer.Serialize(writer, value._value, options);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private class ValueObjectResult
    {
        public ValueObjectInfo? Info { get; set; }
        public System.Collections.Generic.List<Diagnostic> Diagnostics { get; } = new System.Collections.Generic.List<Diagnostic>();
    }

    private class ValueObjectInfo
    {
        public string TypeName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string PrimitiveFullName { get; set; } = string.Empty;
        public bool IsReadonly { get; set; }
        public bool HasValidateMethod { get; set; }
        public bool PrimitiveIsValueType { get; set; }
        public bool PrimitiveRequiresNullCheck { get; set; }
        public bool HasNewMethod { get; set; }
    }
}
