using FluentAssertions;
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
    // NOF010 — must be partial
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

        diagnostics.Should().ContainSingle(d => d.Id == "NOF010");
        result.GeneratedTrees.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // NOF012 — [NewableValueObject] only on ValueObject<long>
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

        diagnostics.Should().ContainSingle(d => d.Id == "NOF012");
        result.GeneratedTrees.Should().BeEmpty();
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

        diagnostics.Should().ContainSingle(d => d.Id == "NOF012");
        result.GeneratedTrees.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Happy path — basic generation
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

        result.GeneratedTrees.Should().ContainSingle();
        var code = GetVoCode(result);

        code.Should().Contain("public static Name Of(string value)");
        code.Should().Contain("global::System.ArgumentNullException.ThrowIfNull(value)");
        code.Should().Contain("public static explicit operator string(Name vo)");
        code.Should().Contain("public static bool operator ==(Name left, Name right)");
        code.Should().Contain("public static bool operator !=(Name left, Name right)");
        code.Should().Contain("public sealed class __JsonConverter");
        code.Should().NotContain("Of(string? value)"); // no nullable overload for ref types
        code.Should().NotContain("public static Name New()");
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
        code.Should().NotContain("ArgumentNullException.ThrowIfNull");
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
        code.Should().Contain("public static OrderId? Of(long? value)");
        code.Should().Contain("value.HasValue ? Of(value.Value) : null");
        code.Should().NotContain("ArgumentNullException.ThrowIfNull");
        code.Should().Contain("_value.GetHashCode()");   // value type — no ?.
        code.Should().Contain("_value.ToString()");
    }

    [Fact]
    public void AlwaysCallsValidateInOf()
    {
        // Validate is a static virtual on IValueObject<T> — always called even without override
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                public readonly partial struct OrderId : IValueObject<long> { }
            }
            """;

        var result = RunGenerator(source);

        var code = GetVoCode(result);
        code.Should().Contain("__CallValidate<OrderId>(value);");
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
        code.Should().Contain("__CallValidate<OrderId>(value);");
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
        code.Should().Contain("public static EntityId New()");
        code.Should().Contain("global::NOF.Domain.IdGenerator.Current.NextId()");
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
        nullGuardIdx.Should().BeLessThan(validateIdx);
    }
}
