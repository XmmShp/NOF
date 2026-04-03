using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NOF.SourceGenerator.Shared;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF.Application.SourceGenerator;

/// <summary>
/// Source generator that discovers <c>partial static class</c> types annotated with
/// <c>[Mappable&lt;TSource, TDest&gt;]</c> or <c>[Mappable(typeof(...), typeof(...))]</c>,
/// and generates an AssemblyInitializer that registers mapping delegates into the global MapperRegistry.
/// <para>
/// Attributes can be scattered across multiple partial declarations of the same class.
/// The generator merges them logically and emits a single AssemblyInitializer for the assembly.
/// </para>
/// </summary>
[Generator]
public class MappableGenerator : IIncrementalGenerator
{
    private const string NonGenericAttributeName = "NOF.Application.MappableAttribute";

    #region Diagnostic descriptors

    private static readonly DiagnosticDescriptor _duplicateMapping = new(
        id: "NOF020",
        title: "Duplicate mapping registration",
        messageFormat: "Mapping from '{0}' to '{1}' is registered more than once (including TwoWay reverse mappings)",
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
        messageFormat: "Property '{0}': mapping between '{1}' and '{2}' has an Optional semantic mismatch and will use IMapper instead",
        category: "NOF.Application",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _unregisteredMapperFallback = new(
        id: "NOF023",
        title: "Mapper fallback to unregistered mapping",
        messageFormat: "Property '{0}': mapping from '{1}' to '{2}' is not auto-generated and requires a manual IMapper registration",
        category: "NOF.Application",
        DiagnosticSeverity.Warning,
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
                var asm = AssemblyPrefixHelper.GetAssemblyPrefix(compilation);
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

                // [Mappable<TSource, TDestination>]
                if (attrType.IsGenericType &&
                    attrType.OriginalDefinition.ToDisplayString() == "NOF.Application.MappableAttribute<TSource, TDestination>")
                {
                    var sourceType = attrType.TypeArguments[0];
                    var destType = attrType.TypeArguments[1];
                    var twoWay = GetNamedBoolArg(attrData, "TwoWay");
                    pairs.Add(new MappingPairInfo(sourceType, destType, twoWay, location));
                }
                // [Mappable(typeof(...), typeof(...))]
                else if (attrType.ToDisplayString() == NonGenericAttributeName &&
                         attrData.ConstructorArguments.Length == 2 &&
                         attrData.ConstructorArguments[0].Value is INamedTypeSymbol src &&
                         attrData.ConstructorArguments[1].Value is INamedTypeSymbol dst)
                {
                    var twoWay = GetNamedBoolArg(attrData, "TwoWay");
                    pairs.Add(new MappingPairInfo(src, dst, twoWay, location));
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

    private static bool GetNamedBoolArg(AttributeData attr, string name)
    {
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == name && arg.Value.Value is bool b)
            {
                return b;
            }
        }
        return false;
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

        var registrationLines = new List<string>();
        var allAutoPairs = new HashSet<(string, string)>();

        foreach (var group in grouped)
        {
            var first = group.First()!;

            if (!first.IsPartialStatic)
            {
                spc.ReportDiagnostic(Diagnostic.Create(_mustBePartialStatic, first.Location, first.TypeName));
                continue;
            }

            var allPairs = group.SelectMany(d => d!.Pairs).ToList();

            var seen = new Dictionary<(string, string), MappingPairInfo>();
            var validPairs = new List<MappingPairInfo>();
            var hasDuplicate = false;

            foreach (var pair in allPairs)
            {
                var fwd = (pair.SourceFullName, pair.DestFullName);
                if (seen.ContainsKey(fwd))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(_duplicateMapping, pair.Location, pair.SourceFullName, pair.DestFullName));
                    hasDuplicate = true;
                    continue;
                }
                seen[fwd] = pair;
                validPairs.Add(pair);

                if (pair.TwoWay)
                {
                    var rev = (pair.DestFullName, pair.SourceFullName);
                    if (seen.ContainsKey(rev))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(_duplicateMapping, pair.Location, pair.DestFullName, pair.SourceFullName));
                        hasDuplicate = true;
                    }
                    else
                    {
                        seen[rev] = pair;
                    }
                }
            }

            if (hasDuplicate || validPairs.Count == 0)
            {
                continue;
            }

            foreach (var p in validPairs)
            {
                allAutoPairs.Add((p.SourceFullName, p.DestFullName));
                if (p.TwoWay)
                {
                    allAutoPairs.Add((p.DestFullName, p.SourceFullName));
                }
            }

            foreach (var pair in validPairs)
            {
                EmitMapping(registrationLines, pair.SourceType, pair.DestType, compilation, allAutoPairs, spc, pair.Location);
                if (pair.TwoWay)
                {
                    EmitMapping(registrationLines, pair.DestType, pair.SourceType, compilation, allAutoPairs, spc, pair.Location);
                }
            }
        }

        if (registrationLines.Count == 0)
        {
            return;
        }

        var sourceText = GenerateAssemblyInitializer(assemblyName, registrationLines);
        spc.AddSource("MapperAssemblyInitializer.g.cs", SourceText.From(sourceText, Encoding.UTF8));
    }

    #endregion

    #region Code generation

    private static readonly SymbolDisplayFormat _fullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);

    private static string GenerateAssemblyInitializer(string assemblyName, List<string> registrations)
    {
        var sanitizedName = assemblyName.Replace(".", "");
        var initializerTypeName = $"__{sanitizedName}MapperAssemblyInitializer";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine();
        sb.AppendLine("[assembly: global::NOF.Annotation.AssemblyInitializeAttribute<global::" + assemblyName + "." + initializerTypeName + ">]");
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

        // Determine if IMapper parameter is needed
        var needsMapper = false;
        foreach (var kvp in ctorMatchedParams)
        {
            if (NeedsMapper(kvp.Value.SrcProp.Type, kvp.Value.Param.Type, compilation))
            {
                needsMapper = true;
                break;
            }
        }
        if (!needsMapper)
        {
            foreach (var (dstProp, srcProp) in initProps)
            {
                if (NeedsMapper(srcProp.Type, dstProp.Type, compilation))
                {
                    needsMapper = true;
                    break;
                }
            }
        }

        var header = needsMapper
            ? $"global::NOF.Application.MapperRegistry.Register<{srcFull}, {dstFull}>((src, mapper) =>"
            : $"global::NOF.Application.MapperRegistry.Register<{srcFull}, {dstFull}>(src =>";

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
                else
                {
                    ctorArgs.Add("default!");
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

        sb.AppendLine(");");
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
    /// 8. Fallback: mapper.Map (with NOF023 warning if pair not auto-generated).
    /// </summary>
    private static string EmitConversion(
        string srcExpr, ITypeSymbol srcType, ITypeSymbol destType, Compilation compilation,
        HashSet<(string, string)> allAutoGeneratedPairs, SourceProductionContext spc,
        Location location, string propertyName)
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
                return EmitMapperFallback(srcExpr, srcType, destType, allAutoGeneratedPairs, spc, location, propertyName);
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
                return EmitMapperFallback(srcExpr, srcType, destType, allAutoGeneratedPairs, spc, location, propertyName);
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
            // VO → anything other than its underlying type: use IMapper
            return EmitMapperFallback(srcExpr, srcType, destType, allAutoGeneratedPairs, spc, location, propertyName);
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
            // Anything other than exact underlying type → VO: use IMapper
            return EmitMapperFallback(srcExpr, srcType, destType, allAutoGeneratedPairs, spc, location, propertyName);
        }

        // --- Nullable<T> unwrap/wrap (value types, including Nullable<VO>) ---
        var srcNullableInner = TryGetNullableUnderlying(srcType);
        var dstNullableInner = TryGetNullableUnderlying(destType);
        if (srcNullableInner != null && dstNullableInner != null)
        {
            // Nullable<T> → Nullable<U>: unwrap, convert, rewrap
            var innerExpr = EmitConversion($"{srcExpr}.Value", srcNullableInner, dstNullableInner,
                compilation, allAutoGeneratedPairs, spc, location, propertyName);
            return $"({srcExpr}.HasValue ? ({innerExpr}) : null)";
        }
        if (srcNullableInner != null)
        {
            // Nullable<T> → U (non-nullable): unwrap and convert
            var innerExpr = EmitConversion($"{srcExpr}.Value", srcNullableInner, destType,
                compilation, allAutoGeneratedPairs, spc, location, propertyName);
            return $"{srcExpr}.HasValue ? {innerExpr} : default";
        }

        // --- IEnumerable<T> → IEnumerable<U> / List<U> / array / custom collection ---
        var srcElemType = TryGetEnumerableElementType(srcType);
        var dstElemType = TryGetEnumerableElementType(destType);
        if (srcElemType != null && dstElemType != null)
        {
            var innerExpr = EmitConversion("_item_", srcElemType, dstElemType,
                compilation, allAutoGeneratedPairs, spc, location, propertyName);
            // Use collection expression [..spread] — lets the compiler pick the right
            // collection type for the target (List<T>, T[], IReadOnlyList<T>, custom, etc.)
            var spreadExpr = innerExpr == "_item_"
                ? srcExpr  // same element type, spread source directly
                : $"{srcExpr}.Select(_item_ => {innerExpr})";
            return $"[..{spreadExpr}]";
        }

        // --- Common primitive conversions ---
        if (TryEmitPrimitiveConversion(srcExpr, srcType, destType, out var primitiveExpr))
        {
            return primitiveExpr;
        }

        // --- Fallback: use IMapper ---
        return EmitMapperFallback(srcExpr, srcType, destType, allAutoGeneratedPairs, spc, location, propertyName);
    }

    private static string EmitMapperFallback(
        string srcExpr, ITypeSymbol srcType, ITypeSymbol destType,
        HashSet<(string, string)> allAutoGeneratedPairs, SourceProductionContext spc,
        Location location, string propertyName)
    {
        var srcFull = srcType.ToDisplayString(_fullyQualifiedFormat);
        var destFull = destType.ToDisplayString(_fullyQualifiedFormat);

        if (!allAutoGeneratedPairs.Contains((srcFull, destFull)))
        {
            spc.ReportDiagnostic(Diagnostic.Create(_unregisteredMapperFallback, location,
                propertyName, srcFull, destFull));
        }

        return $"mapper.Map<{srcFull}, {destFull}>({srcExpr})";
    }

    private static bool NeedsMapper(ITypeSymbol srcType, ITypeSymbol destType, Compilation compilation)
    {
        if (SymbolEqualityComparer.Default.Equals(srcType, destType))
        {
            return false;
        }

        // Implicit conversion (including user-defined) → no mapper
        var conv = compilation.ClassifyConversion(srcType, destType);
        if (conv.IsImplicit)
        {
            return false;
        }

        // User-defined explicit conversion → no mapper (will emit cast)
        if (HasUserDefinedConversion(srcType, destType))
        {
            return false;
        }

        // Optional unwrap: Optional<T> → T? only
        var srcInnerOpt = TryGetOptionalInner(srcType);
        if (srcInnerOpt != null)
        {
            var srcInnerIsNullable = srcInnerOpt.NullableAnnotation == NullableAnnotation.Annotated
                || (srcInnerOpt is INamedTypeSymbol { IsGenericType: true } ns1 && ns1.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);
            var destIsNullable = destType.NullableAnnotation == NullableAnnotation.Annotated
                || (destType is INamedTypeSymbol { IsGenericType: true } nd1 && nd1.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

            if (srcInnerIsNullable || !destIsNullable)
            {
                return true; // semantic mismatch → mapper
            }

            return NeedsMapper(srcInnerOpt, destType, compilation);
        }

        // Result unwrap: same nullable semantics as Optional
        var srcInnerResult = TryGetResultInner(srcType);
        if (srcInnerResult != null)
        {
            var srcResultInnerIsNullable = srcInnerResult.NullableAnnotation == NullableAnnotation.Annotated
                || (srcInnerResult is INamedTypeSymbol { IsGenericType: true } nsr2 && nsr2.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);
            var destIsNullableForResult = destType.NullableAnnotation == NullableAnnotation.Annotated
                || (destType is INamedTypeSymbol { IsGenericType: true } ndr2 && ndr2.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

            if (srcResultInnerIsNullable || !destIsNullableForResult)
            {
                return true; // semantic mismatch → mapper
            }

            return NeedsMapper(srcInnerResult, destType, compilation);
        }

        // VO unwrap: only to exact underlying type
        var srcInnerVo = TryGetValueObjectInner(srcType);
        if (srcInnerVo != null)
        {
            return !EqualsIgnoringNullable(srcInnerVo, destType);
        }

        // VO wrap: only from exact underlying type
        var dstInnerVo = TryGetValueObjectInner(destType);
        if (dstInnerVo != null)
        {
            return !EqualsIgnoringNullable(srcType, dstInnerVo);
        }

        // Nullable<T> → Nullable<U> or Nullable<T> → U
        var srcNullableInner = TryGetNullableUnderlying(srcType);
        var dstNullableInner = TryGetNullableUnderlying(destType);
        if (srcNullableInner != null && dstNullableInner != null)
        {
            return NeedsMapper(srcNullableInner, dstNullableInner, compilation);
        }
        if (srcNullableInner != null)
        {
            return NeedsMapper(srcNullableInner, destType, compilation);
        }

        // IEnumerable<T> → IEnumerable<U> / List<U> / array
        var srcElem = TryGetEnumerableElementType(srcType);
        var dstElem = TryGetEnumerableElementType(destType);
        if (srcElem != null && dstElem != null)
        {
            return NeedsMapper(srcElem, dstElem, compilation);
        }

        if (TryEmitPrimitiveConversion("_", srcType, destType, out _))
        {
            return false;
        }

        return true;
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
        var srcIsString = srcType.SpecialType == SpecialType.System_String;
        var destIsString = destType.SpecialType == SpecialType.System_String;

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

        // T → string (ToString())
        if (destIsString && !srcIsString)
        {
            result = srcType.IsValueType
                ? $"({srcExpr}).ToString()!"
                : $"({srcExpr})?.ToString() ?? \"\"";
            return true;
        }

        // string → numeric (parse)
        if (srcIsString && destIsNumeric)
        {
            result = $"{destFull}.Parse({srcExpr})";
            return true;
        }

        // string → enum (Enum.Parse<T>)
        if (srcIsString && destIsEnum)
        {
            result = $"global::System.Enum.Parse<{destFull}>({srcExpr})";
            return true;
        }

        // enum → enum (cast through int)
        if (srcIsEnum && destIsEnum)
        {
            result = $"({destFull})(int)({srcExpr})";
            return true;
        }

        return false;
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
        public bool TwoWay { get; }
        public Location Location { get; }

        public MappingPairInfo(ITypeSymbol sourceType, ITypeSymbol destType, bool twoWay, Location location)
        {
            SourceType = sourceType;
            DestType = destType;
            SourceFullName = sourceType.ToDisplayString(_fullyQualifiedFormat);
            DestFullName = destType.ToDisplayString(_fullyQualifiedFormat);
            TwoWay = twoWay;
            Location = location;
        }
    }

    #endregion
}
