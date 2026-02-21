using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NOF.Domain;
using NOF.Domain.SourceGenerator;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class ValueObjectGeneratorTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static readonly Type[] ExtraRefs =
    [
        typeof(ValueObjectAttribute<>),
        typeof(NewableValueObjectAttribute),
        typeof(IdGenerator),
    ];

    private static GeneratorDriverRunResult RunGenerator(string source)
        => new ValueObjectGenerator().GetResult(source, ExtraRefs);

    private static (GeneratorDriverRunResult Result, IReadOnlyList<Diagnostic> Diagnostics)
        RunGeneratorWithDiagnostics(string source)
    {
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, ExtraRefs);

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
                [ValueObject<string>]
                public readonly struct NotPartial { }
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);

        diagnostics.Should().ContainSingle(d => d.Id == "NOF010");
        result.GeneratedTrees.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // NOF011 — Validate must be static
    // -----------------------------------------------------------------------

    [Fact]
    public void NonStaticValidate_EmitsNOF011_AndNoSource()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                [ValueObject<string>]
                public readonly partial struct MyVo
                {
                    private void Validate(string value) { }
                }
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);

        diagnostics.Should().ContainSingle(d => d.Id == "NOF011");
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
                [ValueObject<string>]
                [NewableValueObject]
                public readonly partial struct MyVo { }
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
                [ValueObject<int>]
                [NewableValueObject]
                public readonly partial struct MyVo { }
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
                [ValueObject<string>]
                public readonly partial struct Name { }
            }
            """;

        var result = RunGenerator(source);

        result.GeneratedTrees.Should().ContainSingle();
        var code = result.GeneratedTrees[0].GetText().ToString();

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
                [ValueObject<int>]
                public readonly partial struct Score { }
            }
            """;

        var result = RunGenerator(source);

        var code = result.GeneratedTrees[0].GetText().ToString();
        code.Should().NotContain("ArgumentNullException.ThrowIfNull");
    }

    [Fact]
    public void LongVo_GeneratesNullableOverload_AndNoNullGuard()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                [ValueObject<long>]
                public readonly partial struct OrderId { }
            }
            """;

        var result = RunGenerator(source);

        var code = result.GeneratedTrees[0].GetText().ToString();
        code.Should().Contain("public static OrderId? Of(long? value)");
        code.Should().Contain("value.HasValue ? Of(value.Value) : null");
        code.Should().NotContain("ArgumentNullException.ThrowIfNull");
        code.Should().Contain("_value.GetHashCode()");   // value type — no ?.
        code.Should().Contain("_value.ToString()");
    }

    [Fact]
    public void LongVo_WithValidate_CallsValidateInOf()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                [ValueObject<long>]
                public readonly partial struct OrderId
                {
                    private static void Validate(long value) { }
                }
            }
            """;

        var result = RunGenerator(source);

        var code = result.GeneratedTrees[0].GetText().ToString();
        code.Should().Contain("Validate(value);");
    }

    [Fact]
    public void NewableOnLongVo_GeneratesNewMethod()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                [ValueObject<long>]
                [NewableValueObject]
                public readonly partial struct EntityId { }
            }
            """;

        var result = RunGenerator(source);

        var code = result.GeneratedTrees[0].GetText().ToString();
        code.Should().Contain("public static EntityId New()");
        code.Should().Contain("global::NOF.Domain.IdGenerator.Current.NextId()");
    }

    [Fact]
    public void StringVo_WithValidate_CallsValidate_NoNullGuardBeforeValidate()
    {
        const string source = """
            #nullable enable
            using NOF.Domain;
            namespace Test
            {
                [ValueObject<string>]
                public readonly partial struct Tag
                {
                    private static void Validate(string value) { }
                }
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees[0].GetText().ToString();

        // null guard must come before Validate
        var nullGuardIdx = code.IndexOf("ArgumentNullException.ThrowIfNull", StringComparison.Ordinal);
        var validateIdx = code.IndexOf("Validate(value);", StringComparison.Ordinal);
        nullGuardIdx.Should().BeLessThan(validateIdx);
    }
}
