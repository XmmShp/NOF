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
/// Source generator that discovers <c>partial static class</c> types annotated with
/// <c>[Mappable&lt;TSource, TDest&gt;]</c> and generates an assembly initializer that
/// registers mapping expressions into the application mapping registry.
/// <para>
/// Attributes can be scattered across multiple partial declarations of the same class.
/// The generator merges them logically and emits a single AssemblyInitializer for the assembly.
/// </para>
/// </summary>
[Generator]
public class MappableGenerator : IIncrementalGenerator
{

    #region Diagnostic descriptors

    private static readonly DiagnosticDescriptor _duplicateMapping = new(
        id: "NOF020",
        title: "Duplicate mapping registration",
        messageFormat: "Mapping from '{0}' to '{1}' is registered more than once",
        category: "NOF.Application",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _mustBePartialStatic = new(
        id: "NOF021",
        title: "Mappable class must be partial static",
        messageFormat: "Class '{0}' with [Mappable] must be declared as 'partial static class'",
        category: "NOF.Application",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _optionalSemanticMismatch = new(
        id: "NOF022",
        title: "Optional mapping semantic mismatch",
        messageFormat: "Property '{0}': mapping between '{1}' and '{2}' has incompatible optional or nullable semantics",
        category: "NOF.Application",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _missingNestedMapping = new(
        id: "NOF023",
        title: "Nested mapping is not registered",
        messageFormat: "Property '{0}': mapping from '{1}' to '{2}' requires an explicit mapping expression registration",
        category: "NOF.Application",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _incompleteDestination = new(
        id: "NOF024",
        title: "Destination cannot be constructed completely",
        messageFormat: "Destination member or constructor parameter '{0}' on '{1}' has no matching source property",
        category: "NOF.Application",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    #endregion

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var perDeclaration = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, _) => ExtractDeclarationInfo(ctx))
            .Where(static info => info is not null);

        var withAssembly = context.CompilationProvider
            .Combine(perDeclaration.Collect())
            .Select(static (data, _) =>
            {
                var (compilation, decls) = data;
                var asm = compilation.AssemblyName ?? "Unknown";
                return (AssemblyName: asm, Compilation: compilation, Declarations: decls);
            });

        context.RegisterSourceOutput(withAssembly, static (spc, data) =>
        {
            Execute(data.Declarations, data.Compilation, data.AssemblyName, spc);
        });
    }

    #region Extraction

    private static DeclarationInfo? ExtractDeclarationInfo(GeneratorSyntaxContext ctx)
    {
        if (ctx.Node is not ClassDeclarationSyntax cds)
        {
            return null;
        }

        var symbol = ctx.SemanticModel.GetDeclaredSymbol(cds);
        if (symbol is null)
        {
            return null;
        }

        // Only process attributes from THIS specific syntax declaration (not the merged symbol)
        var pairs = new List<MappingPairInfo>();

        foreach (var attrList in cds.AttributeLists)
        {
            foreach (var attrSyntax in attrList.Attributes)
            {
                var attrSymbol = ctx.SemanticModel.GetSymbolInfo(attrSyntax).Symbol as IMethodSymbol;
                var attrType = attrSymbol?.ContainingType;
                if (attrType is null)
                {
                    continue;
                }

                // Match the specific AttributeData from the semantic model
                var attrData = symbol.GetAttributes().FirstOrDefault(a =>
                    a.ApplicationSyntaxReference?.GetSyntax() == attrSyntax);
                if (attrData is null)
                {
                    continue;
                }

                var location = attrSyntax.GetLocation();

                if (attrType.IsGenericType &&
                    attrType.OriginalDefinition.ToDisplayString() == "NOF.Application.MappableAttribute<TSource, TDestination>")
                {
                    var sourceType = attrType.TypeArguments[0];
                    var destType = attrType.TypeArguments[1];
                    pairs.Add(new MappingPairInfo(sourceType, destType, location));
                }
            }
        }

        if (pairs.Count == 0)
        {
            return null;
        }

        var isPartial = cds.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
        var isStatic = cds.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));

        return new DeclarationInfo(
            symbol.Name,
            symbol.ContainingNamespace.ToDisplayString(),
            isPartial && isStatic,
            pairs,
            cds.GetLocation());
    }

    #endregion

    #region Execute

    private static void Execute(ImmutableArray<DeclarationInfo?> declarations, Compilation compilation, string assemblyName, SourceProductionContext spc)
    {
        if (declarations.IsDefaultOrEmpty)
        {
            return;
        }

        var valid = declarations.Where(d => d is not null).ToList();
        if (valid.Count == 0)
        {
            return;
        }

        var grouped = valid.GroupBy(d => new { d!.Namespace, d.TypeName }).ToList();

        var declaredPairs = new List<MappingPairInfo>();

        foreach (var group in grouped)
        {
            var first = group.First()!;

            if (!first.IsPartialStatic)
            {
                spc.ReportDiagnostic(Diagnostic.Create(_mustBePartialStatic, first.Location, first.TypeName));
                continue;
            }

            declaredPairs.AddRange(group.SelectMany(d => d!.Pairs));
        }

        var uniquePairs = new List<MappingPairInfo>();
        var seen = new HashSet<(string, string)>();
        foreach (var pair in declaredPairs)
        {
            if (!seen.Add((pair.SourceFullName, pair.DestFullName)))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    _duplicateMapping,
                    pair.Location,
                    pair.SourceFullName,
                    pair.DestFullName));
                continue;
            }

            uniquePairs.Add(pair);
        }

        var registrationLines = new List<string>();
        var allAutoPairs = new HashSet<(string, string)>(
            uniquePairs.Select(pair => (pair.SourceFullName, pair.DestFullName)));

        foreach (var pair in uniquePairs)
        {
            EmitMapping(
                registrationLines,
                pair.SourceType,
                pair.DestType,
                compilation,
                allAutoPairs,
                spc,
                pair.Location);
        }

        if (registrationLines.Count == 0)
        {
            return;
        }

        var sourceText = GenerateAssemblyInitializer(assemblyName, registrationLines);
        spc.AddSource("MappingAssemblyInitializer.g.cs", SourceText.From(sourceText, Encoding.UTF8));
    }

    #endregion

    #region Code generation

    private static readonly SymbolDisplayFormat _fullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);

    private static string GenerateAssemblyInitializer(string assemblyName, List<string> registrations)
    {
        var sanitizedName = assemblyName.Replace(".", "");
        var initializerTypeName = $"__{sanitizedName}MappingAssemblyInitializer";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine();
        sb.AppendLine("[assembly: global::NOF.Abstraction.AssemblyInitializeAttribute<global::" + assemblyName + "." + initializerTypeName + ">]");
        sb.AppendLine();
        sb.AppendLine($"namespace {assemblyName}");
        sb.AppendLine("{");
        sb.AppendLine($"    internal sealed class {initializerTypeName} : global::NOF.Abstraction.IAssemblyInitializer");
        sb.AppendLine("    {");
        sb.AppendLine("        public static void Initialize(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (!services.InitializedTypes.Add(typeof({initializerTypeName})))");
        sb.AppendLine("            {");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        foreach (var line in registrations)
        {
            sb.Append("            ");
            sb.AppendLine(line);
        }
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitMapping(
        List<string> registrations, ITypeSymbol sourceType, ITypeSymbol destType, Compilation compilation,
        HashSet<(string, string)> allAutoGeneratedPairs, SourceProductionContext spc, Location location)
    {
        var srcFull = sourceType.ToDisplayString(_fullyQualifiedFormat);
        var dstFull = destType.ToDisplayString(_fullyQualifiedFormat);

        if (sourceType.TypeKind == TypeKind.Enum && destType.TypeKind == TypeKind.Enum)
        {
            var expr = EmitConversion("src", sourceType, destType,
                compilation, allAutoGeneratedPairs, spc, location, sourceType.Name, inlineEnumMapping: true);
            registrations.Add(
                $"services.GetOrAddSingleton<global::NOF.Application.MappingRegistry>().Add(global::NOF.Application.MappingRegistration.Of<{srcFull}, {dstFull}>(src => {expr}));");
            return;
        }

        // Collect source properties (public, gettable)
        var srcProps = GetReadableProperties(sourceType);

        // Collect destination writable properties (public init/set)
        var dstWritableProps = GetWritableProperties(destType);

        // Pick the best constructor
        var bestCtor = SelectBestConstructor(destType, srcProps);

        // Figure out which ctor params are matched
        var ctorMatchedParams = new Dictionary<string, (IParameterSymbol Param, IPropertySymbol SrcProp)>(StringComparer.OrdinalIgnoreCase);
        if (bestCtor != null)
        {
            foreach (var param in bestCtor.Parameters)
            {
                var matchingProp = FindMatchingSourceProperty(srcProps, param.Name);
                if (matchingProp != null)
                {
                    ctorMatchedParams[param.Name] = (param, matchingProp);
                }
            }
        }

        if (bestCtor != null)
        {
            var unmatchedParameter = bestCtor.Parameters.FirstOrDefault(parameter =>
                !ctorMatchedParams.ContainsKey(parameter.Name)
                && !parameter.HasExplicitDefaultValue);
            if (unmatchedParameter is not null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    _incompleteDestination,
                    location,
                    unmatchedParameter.Name,
                    dstFull));
                return;
            }
        }

        // Writable properties for member initializer
        var initProps = new List<(IPropertySymbol DstProp, IPropertySymbol SrcProp)>();
        foreach (var dstProp in dstWritableProps)
        {
            var srcProp = srcProps.FirstOrDefault(s => string.Equals(s.Name, dstProp.Name, StringComparison.OrdinalIgnoreCase));
            if (srcProp != null)
            {
                initProps.Add((dstProp, srcProp));
            }
        }

        var unmatchedRequiredProperty = dstWritableProps.FirstOrDefault(property =>
            property.IsRequired
            && !initProps.Any(pair => SymbolEqualityComparer.Default.Equals(pair.DstProp, property))
            && !ctorMatchedParams.ContainsKey(property.Name));
        if (unmatchedRequiredProperty is not null)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                _incompleteDestination,
                location,
                unmatchedRequiredProperty.Name,
                dstFull));
            return;
        }

        var header =
            $"services.GetOrAddSingleton<global::NOF.Application.MappingRegistry>().Add(global::NOF.Application.MappingRegistration.Of<{srcFull}, {dstFull}>(src =>";

        var sb = new StringBuilder();
        sb.AppendLine(header);
        sb.Append($"                new {dstFull}(");

        if (bestCtor != null && bestCtor.Parameters.Length > 0)
        {
            var ctorArgs = new List<string>();
            foreach (var param in bestCtor.Parameters)
            {
                if (ctorMatchedParams.TryGetValue(param.Name, out var match))
                {
                    ctorArgs.Add(EmitConversion($"src.{match.SrcProp.Name}", match.SrcProp.Type, param.Type,
                        compilation, allAutoGeneratedPairs, spc, location, match.SrcProp.Name));
                }
                else if (param.HasExplicitDefaultValue)
                {
                    ctorArgs.Add("default");
                }
            }
            sb.Append(string.Join(", ", ctorArgs));
        }
        sb.Append(')');

        // Member initializer list
        if (initProps.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("                {");
            foreach (var (dstProp, srcProp) in initProps)
            {
                var expr = EmitConversion($"src.{srcProp.Name}", srcProp.Type, dstProp.Type,
                    compilation, allAutoGeneratedPairs, spc, location, srcProp.Name);
                sb.AppendLine($"                    {dstProp.Name} = {expr},");
            }
            sb.Append("                }");
        }

        sb.AppendLine("));");
        registrations.Add(sb.ToString());
    }

    #endregion

    #region Property and constructor matching

    private static List<IPropertySymbol> GetReadableProperties(ITypeSymbol type)
    {
        var result = new List<IPropertySymbol>();
        var current = type;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (current != null)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false, IsIndexer: false } prop &&
                    prop.GetMethod is { DeclaredAccessibility: Accessibility.Public } &&
                    seen.Add(prop.Name))
                {
                    result.Add(prop);
                }
            }
            current = current.BaseType;
        }
        return result;
    }

    private static List<IPropertySymbol> GetWritableProperties(ITypeSymbol type)
    {
        var result = new List<IPropertySymbol>();
        var current = type;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (current != null)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false, IsIndexer: false } prop &&
                    prop.SetMethod is { DeclaredAccessibility: Accessibility.Public } &&
                    seen.Add(prop.Name))
                {
                    result.Add(prop);
                }
            }
            current = current.BaseType;
        }
        return result;
    }

    /// <summary>
    /// Finds the public constructor with the most matched parameters (case-insensitive name match against source properties).
    /// </summary>
    private static IMethodSymbol? SelectBestConstructor(ITypeSymbol destType, List<IPropertySymbol> srcProps)
    {
        if (destType is not INamedTypeSymbol namedType)
        {
            return null;
        }

        IMethodSymbol? best = null;
        var bestMatchCount = -1;

        foreach (var ctor in namedType.InstanceConstructors)
        {
            if (ctor.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            if (ctor.IsImplicitlyDeclared)
            {
                continue;
            }

            var matchCount = 0;
            foreach (var param in ctor.Parameters)
            {
                if (FindMatchingSourceProperty(srcProps, param.Name) != null)
                {
                    matchCount++;
                }
            }

            if (matchCount > bestMatchCount)
            {
                bestMatchCount = matchCount;
                best = ctor;
            }
        }

        return best;
    }

    private static IPropertySymbol? FindMatchingSourceProperty(List<IPropertySymbol> srcProps, string paramName)
    {
        return srcProps.FirstOrDefault(p => string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Type conversion

    /// <summary>
    /// Emits a conversion expression from <paramref name="srcType"/> to <paramref name="destType"/>.
    /// Rules (in order):
    /// 1. Same type → direct assignment.
    /// 2. Implicit conversion (including user-defined) → direct assignment.
    ///    This handles T → Optional&lt;T&gt;, T → Result&lt;T&gt;, etc. via their implicit operators.
    /// 3. User-defined explicit conversion → cast.
    /// 4. Optional unwrap: Optional&lt;T&gt; → T? only. Optional&lt;T?&gt; → T? is unsupported (NOF022).
    ///    Optional&lt;T&gt; → T (non-nullable) is unsupported (NOF022).
    /// 5. Result unwrap: Result&lt;T&gt; → T? only. Same nullable semantics as Optional.
    /// 6. ValueObject unwrap: IValueObject&lt;T&gt; → T only (exact underlying type).
    /// 7. Common primitive conversions (string↔int, int↔enum, etc.).
    /// 8. Fallback: a nested mapping reference (NOF023 when the pair is not declared).
    /// </summary>
    private static string EmitConversion(
        string srcExpr, ITypeSymbol srcType, ITypeSymbol destType, Compilation compilation,
        HashSet<(string, string)> allAutoGeneratedPairs, SourceProductionContext spc,
        Location location, string propertyName, bool inlineEnumMapping = false)
    {
        // Same type — no conversion needed
        if (SymbolEqualityComparer.Default.Equals(srcType, destType))
        {
            return srcExpr;
        }

        // Implicit conversion (including user-defined implicit operators) → direct assignment.
        // This naturally handles T → Optional<T>, T → Result<T> via their implicit operators,
        // following C#'s own implicit conversion rules without special-casing.
        var conv = compilation.ClassifyConversion(srcType, destType);
        if (conv.IsImplicit)
        {
            return srcExpr;
        }

        // User-defined explicit conversion → cast
        if (HasUserDefinedConversion(srcType, destType))
        {
            var destFull = destType.ToDisplayString(_fullyQualifiedFormat);
            return $"(({destFull}){srcExpr})";
        }

        if (srcType.TypeKind == TypeKind.Enum && destType.TypeKind == TypeKind.Enum)
        {
            return inlineEnumMapping
                ? EmitEnumMappingConversion(srcExpr, srcType, destType)
                : EmitNestedMappingReference(srcExpr, srcType, destType, allAutoGeneratedPairs, spc, location, propertyName);
        }

        // --- Unwrap source Optional<T> ---
        var srcInnerFromOptional = TryGetOptionalInner(srcType);
        if (srcInnerFromOptional != null)
        {
            // Optional<T> → T? is allowed (T must not be nullable)
            // Optional<T?> → anything is NOT allowed (semantic mismatch)
            // Optional<T> → T (non-nullable) is NOT allowed (semantic mismatch)
            var srcInnerIsNullable = srcInnerFromOptional.NullableAnnotation == NullableAnnotation.Annotated
                || (srcInnerFromOptional is INamedTypeSymbol { IsGenericType: true } ns && ns.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);
            var destIsNullable = destType.NullableAnnotation == NullableAnnotation.Annotated
                || (destType is INamedTypeSymbol { IsGenericType: true } nd && nd.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

            if (srcInnerIsNullable || !destIsNullable)
            {
                spc.ReportDiagnostic(Diagnostic.Create(_optionalSemanticMismatch, location,
                    propertyName,
                    srcType.ToDisplayString(_fullyQualifiedFormat),
                    destType.ToDisplayString(_fullyQualifiedFormat)));
                return EmitNestedMappingReference(srcExpr, srcType, destType, allAutoGeneratedPairs, spc, location, propertyName);
            }

            // Optional<T> → T? : unwrap via .Value, then recursively convert inner to dest
            return EmitConversion($"{srcExpr}.Value", srcInnerFromOptional, destType,
                compilation, allAutoGeneratedPairs, spc, location, propertyName);
        }

        // --- Unwrap source Result<T> ---
        var srcInnerFromResult = TryGetResultInner(srcType);
        if (srcInnerFromResult != null)
        {
            // Same nullable semantics as Optional
            var srcResultInnerIsNullable = srcInnerFromResult.NullableAnnotation == NullableAnnotation.Annotated
                || (srcInnerFromResult is INamedTypeSymbol { IsGenericType: true } nsr && nsr.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);
            var destIsNullableForResult = destType.NullableAnnotation == NullableAnnotation.Annotated
                || (destType is INamedTypeSymbol { IsGenericType: true } ndr && ndr.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

            if (srcResultInnerIsNullable || !destIsNullableForResult)
            {
                spc.ReportDiagnostic(Diagnostic.Create(_optionalSemanticMismatch, location,
                    propertyName,
                    srcType.ToDisplayString(_fullyQualifiedFormat),
                    destType.ToDisplayString(_fullyQualifiedFormat)));
                return EmitNestedMappingReference(srcExpr, srcType, destType, allAutoGeneratedPairs, spc, location, propertyName);
            }

            // Result<T> → T? : unwrap via .Value!, then recursively convert inner to dest
            return EmitConversion($"{srcExpr}.Value!", srcInnerFromResult, destType,
                compilation, allAutoGeneratedPairs, spc, location, propertyName);
        }

        // --- Unwrap source IValueObject<T> — only to exact underlying type ---
        var srcInnerFromVo = TryGetValueObjectInner(srcType);
        if (srcInnerFromVo != null)
        {
            if (EqualsIgnoringNullable(srcInnerFromVo, destType))
            {
                var innerFull = srcInnerFromVo.ToDisplayString(_fullyQualifiedFormat);
                return $"(({innerFull}){srcExpr})";
            }
            return EmitNestedMappingReference(srcExpr, srcType, destType, allAutoGeneratedPairs, spc, location, propertyName);
        }

        // --- Wrap into destination IValueObject<T> — only from exact underlying type ---
        var dstInnerFromVo = TryGetValueObjectInner(destType);
        if (dstInnerFromVo != null)
        {
            if (EqualsIgnoringNullable(srcType, dstInnerFromVo))
            {
                var dstFull = destType.ToDisplayString(_fullyQualifiedFormat);
                return $"{dstFull}.Of({srcExpr})";
            }
            return EmitNestedMappingReference(srcExpr, srcType, destType, allAutoGeneratedPairs, spc, location, propertyName);
        }

        // --- Nullable<T> unwrap/wrap (value types, including Nullable<VO>) ---
        var srcNullableInner = TryGetNullableUnderlying(srcType);
        var dstNullableInner = TryGetNullableUnderlying(destType);
        if (srcNullableInner != null && dstNullableInner != null)
        {
            // Nullable<T> → Nullable<U>: unwrap, convert, rewrap
            var innerExpr = EmitConversion($"{srcExpr}.Value", srcNullableInner, dstNullableInner,
                compilation, allAutoGeneratedPairs, spc, location, propertyName, inlineEnumMapping);
            return $"({srcExpr}.HasValue ? ({innerExpr}) : null)";
        }
        if (srcNullableInner != null)
        {
            // Nullable<T> → U (non-nullable): unwrap and convert
            var innerExpr = EmitConversion($"{srcExpr}.Value", srcNullableInner, destType,
                compilation, allAutoGeneratedPairs, spc, location, propertyName, inlineEnumMapping);
            return $"{srcExpr}.HasValue ? {innerExpr} : default";
        }

        // --- IEnumerable<T> → IEnumerable<U> / List<U> / array / custom collection ---
        var srcElemType = TryGetEnumerableElementType(srcType);
        var dstElemType = TryGetEnumerableElementType(destType);
        if (srcElemType != null && dstElemType != null)
        {
            var innerExpr = EmitConversion("_item_", srcElemType, dstElemType,
                compilation, allAutoGeneratedPairs, spc, location, propertyName, inlineEnumMapping);
            var projected = innerExpr == "_item_"
                ? srcExpr  // same element type, spread source directly
                : $"{srcExpr}.Select(_item_ => {innerExpr})";

            if (destType is IArrayTypeSymbol)
            {
                return $"{projected}.ToArray()";
            }

            var listDefinition = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
            if (listDefinition is not null)
            {
                var listType = listDefinition.Construct(dstElemType);
                if (compilation.ClassifyConversion(listType, destType).IsImplicit)
                {
                    return $"{projected}.ToList()";
                }
            }

            return EmitNestedMappingReference(
                srcExpr,
                srcType,
                destType,
                allAutoGeneratedPairs,
                spc,
                location,
                propertyName);
        }

        // --- Common primitive and enum conversions ---
        if (TryEmitPrimitiveConversion(srcExpr, srcType, destType, out var primitiveExpr))
        {
            return primitiveExpr;
        }

        return EmitNestedMappingReference(srcExpr, srcType, destType, allAutoGeneratedPairs, spc, location, propertyName);
    }

    private static string EmitNestedMappingReference(
        string srcExpr, ITypeSymbol srcType, ITypeSymbol destType,
        HashSet<(string, string)> allAutoGeneratedPairs, SourceProductionContext spc,
        Location location, string propertyName)
    {
        var srcFull = srcType.ToDisplayString(_fullyQualifiedFormat);
        var destFull = destType.ToDisplayString(_fullyQualifiedFormat);

        if (!allAutoGeneratedPairs.Contains((srcFull, destFull)))
        {
            spc.ReportDiagnostic(Diagnostic.Create(_missingNestedMapping, location,
                propertyName, srcFull, destFull));
        }

        return $"global::NOF.Application.MappingReference.Map<{srcFull}, {destFull}>({srcExpr})";
    }

    #endregion

    #region Wrapper type detection

    /// <summary>
    /// Checks whether there is a user-defined implicit or explicit conversion operator
    /// between the two types (checks both source and destination type members).
    /// </summary>
    private static bool HasUserDefinedConversion(ITypeSymbol srcType, ITypeSymbol destType)
    {
        return HasConversionOperator(srcType, srcType, destType)
            || HasConversionOperator(destType, srcType, destType);
    }

    private static bool HasConversionOperator(ITypeSymbol declaringType, ITypeSymbol srcType, ITypeSymbol destType)
    {
        foreach (var member in declaringType.GetMembers())
        {
            if (member is IMethodSymbol { MethodKind: MethodKind.Conversion } method &&
                method.Parameters.Length == 1 &&
                EqualsIgnoringNullable(method.Parameters[0].Type, srcType) &&
                EqualsIgnoringNullable(method.ReturnType, destType))
            {
                return true;
            }
        }
        return false;
    }

    private static bool EqualsIgnoringNullable(ITypeSymbol a, ITypeSymbol b)
    {
        return SymbolEqualityComparer.Default.Equals(
            a.WithNullableAnnotation(NullableAnnotation.NotAnnotated),
            b.WithNullableAnnotation(NullableAnnotation.NotAnnotated));
    }

    private static ITypeSymbol? TryGetOptionalInner(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } named &&
            named.OriginalDefinition.ToDisplayString() == "NOF.Contract.Optional<T>")
        {
            return named.TypeArguments[0];
        }
        return null;
    }

    private static ITypeSymbol? TryGetResultInner(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } named &&
            named.OriginalDefinition.ToDisplayString() == "NOF.Contract.Result<T>")
        {
            return named.TypeArguments[0];
        }
        return null;
    }

    private static ITypeSymbol? TryGetValueObjectInner(ITypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.IsGenericType &&
                iface.OriginalDefinition.ToDisplayString() == "NOF.Domain.IValueObject<T>")
            {
                return iface.TypeArguments[0];
            }
        }
        return null;
    }

    private static ITypeSymbol? TryGetNullableUnderlying(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } named &&
            named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return named.TypeArguments[0];
        }
        return null;
    }

    private static ITypeSymbol? TryGetEnumerableElementType(ITypeSymbol type)
    {
        // Check if the type itself is IEnumerable<T>
        if (type is INamedTypeSymbol { IsGenericType: true } named &&
            named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
        {
            return named.TypeArguments[0];
        }

        // Check if it's an array
        if (type is IArrayTypeSymbol array)
        {
            return array.ElementType;
        }

        // Check implemented interfaces for IEnumerable<T>
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.IsGenericType &&
                iface.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return iface.TypeArguments[0];
            }
        }
        return null;
    }

    #endregion

    #region Primitive conversions

    private static bool TryEmitPrimitiveConversion(string srcExpr, ITypeSymbol srcType, ITypeSymbol destType, out string result)
    {
        result = "";

        var destFull = destType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var srcIsEnum = srcType.TypeKind == TypeKind.Enum;
        var destIsEnum = destType.TypeKind == TypeKind.Enum;
        var srcIsNumeric = IsNumericType(srcType);
        var destIsNumeric = IsNumericType(destType);
        // numeric → numeric (cast)
        if (srcIsNumeric && destIsNumeric)
        {
            result = $"({destFull})({srcExpr})";
            return true;
        }

        // enum → numeric (cast)
        if (srcIsEnum && destIsNumeric)
        {
            result = $"({destFull})({srcExpr})";
            return true;
        }

        // numeric → enum (cast)
        if (srcIsNumeric && destIsEnum)
        {
            result = $"({destFull})({srcExpr})";
            return true;
        }

        return false;
    }

    private static string EmitEnumMappingConversion(string srcExpr, ITypeSymbol srcType, ITypeSymbol destType)
    {
        var destFull = destType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var mappings = new List<(IFieldSymbol Source, IFieldSymbol Destination)>();
        var destMembers = GetEnumMembers(destType);

        foreach (var srcMember in GetEnumMembers(srcType))
        {
            var destMember = destMembers.FirstOrDefault(member =>
                string.Equals(member.Name, srcMember.Name, StringComparison.Ordinal));
            destMember ??= destMembers.FirstOrDefault(member =>
                string.Equals(member.Name, srcMember.Name, StringComparison.OrdinalIgnoreCase));

            if (destMember is null)
            {
                continue;
            }

            mappings.Add((srcMember, destMember));
        }

        var result = $"({destFull})(int)({srcExpr})";
        for (var index = mappings.Count - 1; index >= 0; index--)
        {
            var mapping = mappings[index];
            result = $"{srcExpr} == {EmitEnumMemberReference(mapping.Source)} ? " +
                $"{EmitEnumMemberReference(mapping.Destination)} : ({result})";
        }

        return result;
    }

    private static string EmitEnumMemberReference(IFieldSymbol member)
    {
        return $"{member.ContainingType.ToDisplayString(_fullyQualifiedFormat)}.{EscapeIdentifier(member.Name)}";
    }

    private static string EscapeIdentifier(string identifier)
    {
        return SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None
            ? identifier
            : "@" + identifier;
    }

    private static List<IFieldSymbol> GetEnumMembers(ITypeSymbol type)
    {
        return type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => field is
            {
                DeclaredAccessibility: Accessibility.Public,
                IsStatic: true,
                HasConstantValue: true,
                IsImplicitlyDeclared: false
            })
            .ToList();
    }

    private static bool IsNumericType(ITypeSymbol type)
    {
        return type.SpecialType is
            SpecialType.System_Byte or SpecialType.System_SByte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or
            SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or
            SpecialType.System_Single or SpecialType.System_Double or
            SpecialType.System_Decimal;
    }

    #endregion

    #region Data types

    private class DeclarationInfo
    {
        public string TypeName { get; }
        public string Namespace { get; }
        public bool IsPartialStatic { get; }
        public List<MappingPairInfo> Pairs { get; }
        public Location Location { get; }

        public DeclarationInfo(string typeName, string ns, bool isPartialStatic, List<MappingPairInfo> pairs, Location location)
        {
            TypeName = typeName;
            Namespace = ns;
            IsPartialStatic = isPartialStatic;
            Pairs = pairs;
            Location = location;
        }
    }

    private class MappingPairInfo
    {
        public ITypeSymbol SourceType { get; }
        public ITypeSymbol DestType { get; }
        public string SourceFullName { get; }
        public string DestFullName { get; }
        public Location Location { get; }

        public MappingPairInfo(ITypeSymbol sourceType, ITypeSymbol destType, Location location)
        {
            SourceType = sourceType;
            DestType = destType;
            SourceFullName = sourceType.ToDisplayString(_fullyQualifiedFormat);
            DestFullName = destType.ToDisplayString(_fullyQualifiedFormat);
            Location = location;
        }
    }

    #endregion
}
