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
        Assert.DoesNotContain("Of(string? value)", code); // no nullable overload for ref types
        Assert.DoesNotContain("public static Name New()", code);
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
        Assert.Contains("_value.ToString()", code);
    }

    [Fact]
    public void AlwaysCallsValidateInOf()
    {
        // Validate is a static virtual on IValueObject<T> 鈥?always called even without override
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                public readonly partial struct OrderId : IValueObject<long> { }
            }
            """;

        var result = RunGenerator(source);

        var code = GetVoCode(result);
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
        Assert.Contains("__CallValidate<OrderId>(value);", code);
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
        Assert.Contains("public static EntityId New()", code);
        Assert.Contains("global::NOF.Domain.IdGenerator.Current.NextId()", code);
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

        // null guard must come before Validate
        var nullGuardIdx = code.IndexOf("ArgumentNullException.ThrowIfNull", StringComparison.Ordinal);
        var validateIdx = code.IndexOf("__CallValidate<Tag>(value);", StringComparison.Ordinal);
        Assert.True(nullGuardIdx < validateIdx);
    }
}



