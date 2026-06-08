using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NOF.Domain;
using NOF.Domain.SourceGenerator;
using NOF.SourceGenerator.Tests.Extensions;
using System.Text.Json.Serialization;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class ValueObjectGeneratorTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static readonly Type[] _extraRefs =
    [
        typeof(IValueObject<>),
        typeof(NewableValueObjectAttribute),
        typeof(IdGenerator),
        typeof(JsonConverterAttribute),
    ];

    private static GeneratorDriverRunResult RunGenerator(string source)
        => new ValueObjectGenerator().GetResultPostGen(source, _extraRefs);

    private static string GetVoCode(GeneratorDriverRunResult result)
        => result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .Single();

    private static (GeneratorDriverRunResult Result, IReadOnlyList<Diagnostic> Diagnostics)
        RunGeneratorWithDiagnostics(string source)
    {
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, _extraRefs);

        var driver = CSharpGeneratorDriver.Create(new ValueObjectGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

        var result = driver.GetRunResult();
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToList();
        return (result, diagnostics);
    }

    // -----------------------------------------------------------------------
    // NOF010 鈥?must be partial
    // -----------------------------------------------------------------------

    [Fact]
    public void NonPartialStruct_EmitsNOF010_AndNoSource()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                public readonly struct NotPartial : IValueObject<string> { }
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);

        Assert.Single(diagnostics, d => d.Id == "NOF010");
        Assert.Empty(
        result.GeneratedTrees);
    }

    // -----------------------------------------------------------------------
    // NOF012 鈥?[NewableValueObject] only on ValueObject<long>
    // -----------------------------------------------------------------------

    [Fact]
    public void NewableOnStringVo_EmitsNOF012_AndNoSource()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                [NewableValueObject]
                public readonly partial struct MyVo : IValueObject<string> { }
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);

        Assert.Single(diagnostics, d => d.Id == "NOF012");
        Assert.Empty(
        result.GeneratedTrees);
    }

    [Fact]
    public void NewableOnIntVo_EmitsNOF012_AndNoSource()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                [NewableValueObject]
                public readonly partial struct MyVo : IValueObject<int> { }
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);

        Assert.Single(diagnostics, d => d.Id == "NOF012");
        Assert.Empty(
        result.GeneratedTrees);
    }

    // -----------------------------------------------------------------------
    // Happy path 鈥?basic generation
    // -----------------------------------------------------------------------

    [Fact]
    public void StringVo_GeneratesExpectedMembers()
    {
        const string source = """
            #nullable enable
            using NOF.Domain;
            namespace Test
            {
                public readonly partial struct Name : IValueObject<string> { }
            }
            """;

        var result = RunGenerator(source);

        Assert.Single(result.GeneratedTrees);
        var code = GetVoCode(result);

        Assert.Contains("public static Name Of(string value)", code);
        Assert.Contains("global::System.ArgumentNullException.ThrowIfNull(value)", code);
        Assert.Contains("public static explicit operator string(Name vo)", code);
        Assert.Contains("public static bool operator ==(Name left, Name right)", code);
        Assert.Contains("public static bool operator !=(Name left, Name right)", code);
        Assert.Contains("public sealed class __JsonConverter", code);
        Assert.Contains("private static global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<string> __GetPrimitiveJsonTypeInfo", code);
        Assert.Contains("JsonSerializer.Deserialize(ref reader, __GetPrimitiveJsonTypeInfo(options))", code);
        Assert.Contains("JsonSerializer.Serialize(writer, value._value, __GetPrimitiveJsonTypeInfo(options))", code);
        Assert.Contains("public override string ToString()", code);
        Assert.DoesNotContain("Of(string? value)", code); // no nullable overload for ref types
        Assert.DoesNotContain("public static Name New()", code);
        Assert.DoesNotContain("public override string? ToString()", code);
    }

    [Fact]
    public void ValueTypeVo_DoesNotEmitNullGuard()
    {
        // Value types (int, long, etc.) never need a null guard
        const string source = """
            #nullable enable
            using NOF.Domain;
            namespace Test
            {
                public readonly partial struct Score : IValueObject<int> { }
            }
            """;

        var result = RunGenerator(source);

        var code = GetVoCode(result);
        Assert.DoesNotContain("ArgumentNullException.ThrowIfNull", code);
    }

    [Fact]
    public void LongVo_GeneratesNullableOverload_AndNoNullGuard()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                public readonly partial struct OrderId : IValueObject<long> { }
            }
            """;

        var result = RunGenerator(source);

        var code = GetVoCode(result);
        Assert.Contains("public static OrderId? Of(long? value)", code);
        Assert.Contains("value.HasValue ? Of(value.Value) : null", code);
        Assert.DoesNotContain("ArgumentNullException.ThrowIfNull", code);
        Assert.Contains("_value.GetHashCode()", code);   // value type 鈥?no ?.
        Assert.Contains("public override string ToString()", code);
        Assert.Contains("_value.ToString() ?? string.Empty", code);
    }

    [Fact]
    public void NullableReferenceUnderlyingType_EmitsNOF011_ButStillGeneratesSource()
    {
        const string source = """
            #nullable enable
            using NOF.Domain;
            namespace Test
            {
                public readonly partial struct Name : IValueObject<string?> { }
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == "NOF011" && d.Severity == DiagnosticSeverity.Warning);
        Assert.Single(result.GeneratedTrees);
    }

    [Fact]
    public void NullableValueUnderlyingType_EmitsNOF011_ButStillGeneratesSource()
    {
        const string source = """
            #nullable enable
            using NOF.Domain;
            namespace Test
            {
                public readonly partial struct Score : IValueObject<int?> { }
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == "NOF011" && d.Severity == DiagnosticSeverity.Warning);
        Assert.Single(result.GeneratedTrees);
    }

    [Fact]
    public void AlwaysCallsValidateInOf()
    {
        // Validate is a static virtual on IValueObject<T> and is always called after Normalize.
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                public readonly partial struct OrderId : IValueObject<long> { }
            }
            """;

        var result = RunGenerator(source);

        var code = GetVoCode(result);
        Assert.Contains("value = __CallNormalize<OrderId>(value);", code);
        Assert.Contains("__CallValidate<OrderId>(value);", code);
    }

    [Fact]
    public void UserOverriddenValidate_IsCalledInOf()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                public readonly partial struct OrderId : IValueObject<long>
                {
                    public static void Validate(long value)
                    {
                        if (value <= 0) throw new System.ArgumentException("must be positive");
                    }
                }
            }
            """;

        var result = RunGenerator(source);

        var code = GetVoCode(result);
        Assert.Contains("value = __CallNormalize<OrderId>(value);", code);
        Assert.Contains("__CallValidate<OrderId>(value);", code);
    }

    [Fact]
    public void Normalize_IsCalledBeforeValidate()
    {
        const string source = """
            #nullable enable
            using NOF.Domain;
            namespace Test
            {
                public readonly partial struct Name : IValueObject<string>
                {
                    public static string Normalize(string value) => value.Trim();
                }
            }
            """;

        var result = RunGenerator(source);
        var code = GetVoCode(result);

        var normalizeIdx = code.IndexOf("__CallNormalize<Name>(value);", StringComparison.Ordinal);
        var validateIdx = code.IndexOf("__CallValidate<Name>(value);", StringComparison.Ordinal);
        Assert.True(normalizeIdx >= 0);
        Assert.True(validateIdx > normalizeIdx);
    }

    [Fact]
    public void NewableOnLongVo_GeneratesNewMethod()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                [NewableValueObject]
                public readonly partial struct EntityId : IValueObject<long> { }
            }
            """;

        var result = RunGenerator(source);

        var code = GetVoCode(result);
        Assert.Contains("public static EntityId New(global::NOF.Domain.IIdGenerator generator)", code);
        Assert.Contains("global::System.ArgumentNullException.ThrowIfNull(generator);", code);
        Assert.Contains("return Of(generator.NextId());", code);
        Assert.Contains("public static EntityId New()", code);
        Assert.Contains("=> New(global::NOF.Domain.IdGenerator.Current);", code);
    }

    [Fact]
    public void StringVo_NullGuardBeforeValidate()
    {
        const string source = """
            #nullable enable
            using NOF.Domain;
            namespace Test
            {
                public readonly partial struct Tag : IValueObject<string> { }
            }
            """;

        var result = RunGenerator(source);
        var code = GetVoCode(result);

        // null guard must come before Normalize/Validate
        var nullGuardIdx = code.IndexOf("ArgumentNullException.ThrowIfNull", StringComparison.Ordinal);
        var normalizeIdx = code.IndexOf("__CallNormalize<Tag>(value);", StringComparison.Ordinal);
        var validateIdx = code.IndexOf("__CallValidate<Tag>(value);", StringComparison.Ordinal);
        Assert.True(nullGuardIdx < normalizeIdx);
        Assert.True(normalizeIdx < validateIdx);
    }
}
