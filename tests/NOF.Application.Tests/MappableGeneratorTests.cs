using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NOF.Application;
using NOF.Application.SourceGenerator;
using NOF.Contract;
using NOF.Domain;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class MappableGeneratorTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static readonly Type[] _extraRefs =
    [
        typeof(MappableAttribute),
        typeof(IMapper),
        typeof(MapperRegistry),
        typeof(Contract.Optional<>),
        typeof(Result),
        typeof(IValueObject<>),
    ];

    private static GeneratorDriverRunResult RunGenerator(string source)
        => new MappableGenerator().GetResult(source, _extraRefs);

    private static (GeneratorDriverRunResult Result, IReadOnlyList<Diagnostic> Diagnostics)
        RunGeneratorWithDiagnostics(string source)
    {
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, _extraRefs);

        var driver = CSharpGeneratorDriver.Create(new MappableGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

        var result = driver.GetRunResult();
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToList();
        return (result, diagnostics);
    }

    // -----------------------------------------------------------------------
    // Basic: simple property mapping (same names, same types)
    // -----------------------------------------------------------------------

    [Fact]
    public void SimplePropertyMapping_GeneratesAdd()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public class Order { public int Id { get; set; } public string Name { get; set; } }
                public class OrderDto { public int Id { get; set; } public string Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        Assert.Single(result.GeneratedTrees);

        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("MapperRegistry.Register<", code);
        Assert.Contains("MapperAssemblyInitializer", code);
        Assert.Contains("Id = src.Id", code);
        Assert.Contains("Name = src.Name", code);
    }

    // -----------------------------------------------------------------------
    // Constructor matching: record with primary ctor
    // -----------------------------------------------------------------------

    [Fact]
    public void RecordConstructorMatching_UsesCtorParams()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public class Order { public int Id { get; set; } public string Name { get; set; } }
                public record OrderDto(int Id, string Name);

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("new global::Test.OrderDto(src.Id, src.Name)", code);
    }

    // -----------------------------------------------------------------------
    // TwoWay generates both directions
    // -----------------------------------------------------------------------

    [Fact]
    public void TwoWay_GeneratesBothDirections()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public class Order { public int Id { get; set; } }
                public class OrderDto { public int Id { get; set; } }

                [Mappable<Order, OrderDto>(TwoWay = true)]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("MapperRegistry.Register<global::Test.Order, global::Test.OrderDto>", code);
        Assert.Contains("MapperRegistry.Register<global::Test.OrderDto, global::Test.Order>", code);
    }

    // -----------------------------------------------------------------------
    // Duplicate mapping emits NOF020
    // -----------------------------------------------------------------------

    [Fact]
    public void DuplicateMapping_EmitsNOF020()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public class A { public int Id { get; set; } }
                public class B { public int Id { get; set; } }

                [Mappable<A, B>]
                [Mappable<A, B>]
                public static partial class Mappings;
            }
            """;

        var (_, diagnostics) = RunGeneratorWithDiagnostics(source);
        Assert.Contains(diagnostics, d => d.Id == "NOF020");
    }

    // -----------------------------------------------------------------------
    // TwoWay duplicate: A閳墪 + B閳墣 explicit = duplicate
    // -----------------------------------------------------------------------

    [Fact]
    public void TwoWayDuplicate_EmitsNOF020()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public class A { public int Id { get; set; } }
                public class B { public int Id { get; set; } }

                [Mappable<A, B>(TwoWay = true)]
                [Mappable<B, A>]
                public static partial class Mappings;
            }
            """;

        var (_, diagnostics) = RunGeneratorWithDiagnostics(source);
        Assert.Contains(diagnostics, d => d.Id == "NOF020");
    }

    // -----------------------------------------------------------------------
    // Non-partial-static class emits NOF021
    // -----------------------------------------------------------------------

    [Fact]
    public void NonPartialStatic_EmitsNOF021()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public class A { public int Id { get; set; } }
                public class B { public int Id { get; set; } }

                [Mappable<A, B>]
                public static class Mappings;
            }
            """;

        var (_, diagnostics) = RunGeneratorWithDiagnostics(source);
        Assert.Contains(diagnostics, d => d.Id == "NOF021");
    }

    // -----------------------------------------------------------------------
    // Non-generic attribute [Mappable(typeof(A), typeof(B))]
    // -----------------------------------------------------------------------

    [Fact]
    public void NonGenericAttribute_Works()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public class Order { public int Id { get; set; } }
                public class OrderDto { public int Id { get; set; } }

                [Mappable(typeof(Order), typeof(OrderDto))]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("MapperRegistry.Register<global::Test.Order, global::Test.OrderDto>", code);
    }

    // -----------------------------------------------------------------------
    // Partial declarations merge into one method
    // -----------------------------------------------------------------------

    [Fact]
    public void PartialDeclarations_MergeIntoOneMethod()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public class A { public int Id { get; set; } }
                public class B { public int Id { get; set; } }
                public class C { public int Id { get; set; } }

                [Mappable<A, B>]
                public static partial class Mappings;

                [Mappable<A, C>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        // Should produce exactly one generated file for the class
        Assert.Single(result.GeneratedTrees);

        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("MapperRegistry.Register<global::Test.A, global::Test.B>", code);
        Assert.Contains("MapperRegistry.Register<global::Test.A, global::Test.C>", code);
        // One method
        Assert.Contains("MapperAssemblyInitializer", code);
    }

    // -----------------------------------------------------------------------
    // int 閳?string conversion (ToString)
    // -----------------------------------------------------------------------

    [Fact]
    public void IntToString_GeneratesToString()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public class Order { public int Id { get; set; } }
                public class OrderDto { public string Id { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("ToString()", code);
    }

    // -----------------------------------------------------------------------
    // string 閳?int conversion (Parse)
    // -----------------------------------------------------------------------

    [Fact]
    public void StringToInt_GeneratesParse()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public class Order { public string Id { get; set; } }
                public class OrderDto { public int Id { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("Parse(src.Id)", code);
    }

    // -----------------------------------------------------------------------
    // Enum conversion: int 閳?enum, enum 閳?string
    // -----------------------------------------------------------------------

    [Fact]
    public void EnumConversions_GenerateCastOrParse()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public enum Status { Active, Inactive }
                public class Order { public int StatusCode { get; set; } public Status Status { get; set; } }
                public class OrderDto { public Status StatusCode { get; set; } public string Status { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // int 閳?enum: cast
        Assert.Contains("(global::Test.Status)(src.StatusCode)", code);
        // enum 閳?string: ToString
        Assert.Contains("src.Status", code);
        Assert.Contains("ToString()", code);
    }

    // -----------------------------------------------------------------------
    // Unmapped properties use IMapper fallback
    // -----------------------------------------------------------------------

    [Fact]
    public void UnknownType_FallsBackToMapper()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public class Address { public string Street { get; set; } }
                public class AddressDto { public string Street { get; set; } }
                public class Order { public Address Addr { get; set; } }
                public class OrderDto { public AddressDto Addr { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("mapper.Map<global::Test.Address, global::Test.AddressDto>(src.Addr)", code);
        // Should use the (src, mapper) overload
        Assert.Contains("(src, mapper) =>", code);
    }

    // -----------------------------------------------------------------------
    // Best constructor selection: picks ctor with most matches
    // -----------------------------------------------------------------------

    [Fact]
    public void BestConstructorSelection_PicksMostMatched()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public class Order { public int Id { get; set; } public string Name { get; set; } public string Code { get; set; } }
                public class OrderDto
                {
                    public OrderDto(int id) { Id = id; }
                    public OrderDto(int id, string name) { Id = id; Name = name; }
                    public int Id { get; set; }
                    public string Name { get; set; }
                    public string Code { get; set; }
                }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // Should pick the 2-param constructor (id, name) since it matches more
        Assert.Contains("new global::Test.OrderDto(src.Id, src.Name)", code);
        // Code should still appear in init list since it's writable
        Assert.Contains("Code = src.Code", code);
    }

    // -----------------------------------------------------------------------
    // No source generated when no [Mappable] attributes
    // -----------------------------------------------------------------------

    [Fact]
    public void NoAttributes_NoGeneration()
    {
        const string source = """
            namespace Test
            {
                public class Order { public int Id { get; set; } }
            }
            """;

        var result = RunGenerator(source);
        Assert.Empty(
        result.GeneratedTrees);
    }

    // =======================================================================
    //  Optional<T> tests
    // =======================================================================

    // -----------------------------------------------------------------------
    // Optional<T> 閳?T? : valid unwrap via .Value
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalUnwrap_ToNullable_GeneratesDotValue()
    {
        const string source = """
            #nullable enable
            using NOF.Application;
            using NOF.Contract;
            namespace Test
            {
                public class Order { public Optional<string> Name { get; set; } }
                public class OrderDto { public string? Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("src.Name.Value", code);
    }

    // -----------------------------------------------------------------------
    // Optional<T> 閳?T (non-nullable): semantic mismatch 閳?NOF022 warning + mapper fallback
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalUnwrap_ToNonNullable_EmitsWarningNOF022()
    {
        const string source = """
            #nullable enable
            using NOF.Application;
            using NOF.Contract;
            namespace Test
            {
                public class Order { public Optional<string> Name { get; set; } }
                public class OrderDto { public string Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);
        Assert.Contains(diagnostics, d => d.Id == "NOF022");
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("mapper.Map<", code);
    }

    // -----------------------------------------------------------------------
    // T 閳?Optional<T>  (implicit conversion 閳?direct assignment)
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalWrap_ImplicitConversion()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;
            namespace Test
            {
                public class Order { public string Name { get; set; } }
                public class OrderDto { public Optional<string> Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // T 閳?Optional<T> via implicit operator 閳?direct assignment, no Optional.Of
        Assert.Contains("Name = src.Name", code);
        Assert.DoesNotContain("Optional.Of", code);
    }

    // -----------------------------------------------------------------------
    // Optional<T> 閳?Optional<T>  (same type, no conversion)
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalToOptional_SameType_NoConversion()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;
            namespace Test
            {
                public class Order { public Optional<int> Score { get; set; } }
                public class OrderDto { public Optional<int> Score { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // Same type 閳?direct assignment, no .Value or Optional.Of
        Assert.Contains("Score = src.Score", code);
        Assert.DoesNotContain("Optional.Of", code);
    }

    // =======================================================================
    //  Result<T> tests (same 8-case nullable semantics as Optional<T>)
    // =======================================================================

    // -----------------------------------------------------------------------
    // Result<T> 閳?T? : valid unwrap via .Value!
    // -----------------------------------------------------------------------

    [Fact]
    public void ResultUnwrap_ToNullable_GeneratesDotValueBang()
    {
        const string source = """
            #nullable enable
            using NOF.Application;
            using NOF.Contract;
            namespace Test
            {
                public class Order { public Result<string> Name { get; set; } }
                public class OrderDto { public string? Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("src.Name.Value!", code);
    }

    // -----------------------------------------------------------------------
    // Result<T> 閳?T (non-nullable): semantic mismatch 閳?NOF022
    // -----------------------------------------------------------------------

    [Fact]
    public void ResultUnwrap_ToNonNullable_EmitsWarningNOF022()
    {
        const string source = """
            #nullable enable
            using NOF.Application;
            using NOF.Contract;
            namespace Test
            {
                public class Order { public Result<string> Name { get; set; } }
                public class OrderDto { public string Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);
        Assert.Contains(diagnostics, d => d.Id == "NOF022");
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("mapper.Map<", code);
    }

    // -----------------------------------------------------------------------
    // T 閳?Result<T>  (wrap via implicit conversion)
    // -----------------------------------------------------------------------

    [Fact]
    public void ResultWrap_ImplicitConversion()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;
            namespace Test
            {
                public class Order { public string Name { get; set; } }
                public class OrderDto { public Result<string> Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // T 閳?Result<T> is ok (implicit conversion)
        Assert.Contains("Name = src.Name", code);
        Assert.DoesNotContain(".Value!", code);
    }

    // -----------------------------------------------------------------------
    // T 閳?Result<T?>  (implicit conversion 閳?direct assignment)
    // -----------------------------------------------------------------------

    [Fact]
    public void ResultWrap_ToNullableInner_Ok()
    {
        const string source = """
            #nullable enable
            using NOF.Application;
            using NOF.Contract;
            namespace Test
            {
                public class Order { public string Name { get; set; } }
                public class OrderDto { public Result<string?> Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("Name = src.Name", code);
    }

    // -----------------------------------------------------------------------
    // T? 閳?Result<T> : follows C# implicit conversion rules (direct assignment)
    // -----------------------------------------------------------------------

    [Fact]
    public void ResultWrap_NullableToNonNullableInner_ImplicitConversion()
    {
        const string source = """
            #nullable enable
            using NOF.Application;
            using NOF.Contract;
            namespace Test
            {
                public class Order { public string? Name { get; set; } }
                public class OrderDto { public Result<string> Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        // C# implicit operator Result<T>(T value) accepts string? 閳?Result<string>
        // We follow C#'s own implicit conversion rules 閳?no special-casing
        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("Name = src.Name", code);
    }

    // -----------------------------------------------------------------------
    // T? 閳?Result<T?> : implicit conversion (direct assignment)
    // -----------------------------------------------------------------------

    [Fact]
    public void ResultWrap_NullableToNullableInner_Ok()
    {
        const string source = """
            #nullable enable
            using NOF.Application;
            using NOF.Contract;
            namespace Test
            {
                public class Order { public string? Name { get; set; } }
                public class OrderDto { public Result<string?> Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("Name = src.Name", code);
    }

    // -----------------------------------------------------------------------
    // Result<T?> 閳?T? : semantic mismatch 閳?NOF022
    // -----------------------------------------------------------------------

    [Fact]
    public void ResultUnwrap_NullableInnerToNullable_EmitsNOF022()
    {
        const string source = """
            #nullable enable
            using NOF.Application;
            using NOF.Contract;
            namespace Test
            {
                public class Order { public Result<string?> Name { get; set; } }
                public class OrderDto { public string? Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);
        Assert.Contains(diagnostics, d => d.Id == "NOF022");
    }

    // =======================================================================
    //  IValueObject<T> tests
    // =======================================================================

    // We define a hand-written value object in test source that implements IValueObject<T>
    // with the standard members (Of, explicit cast). The generator only checks AllInterfaces.

    private const string ValueObjectDefs = """
        public readonly struct OrderName : IValueObject<string>
        {
            private readonly string _value;
            private OrderName(string value) { _value = value; }
            public static OrderName Of(string value) => new(value);
            public static explicit operator string(OrderName vo) => vo._value;
            public string GetUnderlyingValue() => _value;
        }
        """;

    // -----------------------------------------------------------------------
    // IValueObject<T> 閳?T  (unwrap via explicit cast, exact underlying type)
    // -----------------------------------------------------------------------

    [Fact]
    public void ValueObjectUnwrap_GeneratesExplicitCast()
    {
        var source = $$"""
            using NOF.Application;
            using NOF.Domain;
            namespace Test
            {
                {{ValueObjectDefs}}
                public class Order { public OrderName Name { get; set; } }
                public class OrderDto { public string Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // Should unwrap via explicit cast to the underlying type
        Assert.Contains("(string)src.Name)", code);
    }

    // -----------------------------------------------------------------------
    // T 閳?IValueObject<T>  (wrap via VoType.Of, exact underlying type)
    // -----------------------------------------------------------------------

    [Fact]
    public void ValueObjectWrap_GeneratesOf()
    {
        var source = $$"""
            using NOF.Application;
            using NOF.Domain;
            namespace Test
            {
                {{ValueObjectDefs}}
                public class Order { public string Name { get; set; } }
                public class OrderDto { public OrderName Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // Should wrap via OrderName.Of(...)
        Assert.Contains("OrderName.Of(src.Name)", code);
    }

    // -----------------------------------------------------------------------
    // IValueObject<string> 閳?int : VO to non-underlying type 閳?mapper fallback + NOF023
    // -----------------------------------------------------------------------

    [Fact]
    public void ValueObjectToNonUnderlyingType_FallsBackToMapper_NOF023()
    {
        var source = $$"""
            using NOF.Application;
            using NOF.Domain;
            namespace Test
            {
                {{ValueObjectDefs}}
                public class Order { public OrderName Code { get; set; } }
                public class OrderDto { public int Code { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);
        Assert.Contains(diagnostics, d => d.Id == "NOF023");
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("mapper.Map<", code);
        Assert.DoesNotContain("Parse(", code);
    }

    // -----------------------------------------------------------------------
    // int 閳?IValueObject<int>  (wrap via Of with direct value)
    // -----------------------------------------------------------------------

    [Fact]
    public void IntToValueObject_WrapsViaOf()
    {
        const string source = """
            using NOF.Application;
            using NOF.Domain;
            namespace Test
            {
                public readonly struct Score : IValueObject<int>
                {
                    private readonly int _value;
                    private Score(int value) { _value = value; }
                    public static Score Of(int value) => new(value);
                    public static explicit operator int(Score vo) => vo._value;
                    public int GetUnderlyingValue() => _value;
                }

                public class Order { public int Score { get; set; } }
                public class OrderDto { public Score Score { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("Score.Of(src.Score)", code);
    }

    // -----------------------------------------------------------------------
    // IValueObject<T> 閳?IValueObject<T> (different VO types): fallback to mapper + NOF023
    // -----------------------------------------------------------------------

    [Fact]
    public void ValueObjectToValueObject_DifferentVoTypes_FallsBackToMapper()
    {
        const string source = """
            using NOF.Application;
            using NOF.Domain;
            namespace Test
            {
                public readonly struct FirstName : IValueObject<string>
                {
                    private readonly string _value;
                    private FirstName(string value) { _value = value; }
                    public static FirstName Of(string value) => new(value);
                    public static explicit operator string(FirstName vo) => vo._value;
                    public string GetUnderlyingValue() => _value;
                }
                public readonly struct DisplayName : IValueObject<string>
                {
                    private readonly string _value;
                    private DisplayName(string value) { _value = value; }
                    public static DisplayName Of(string value) => new(value);
                    public static explicit operator string(DisplayName vo) => vo._value;
                    public string GetUnderlyingValue() => _value;
                }

                public class Order { public FirstName Name { get; set; } }
                public class OrderDto { public DisplayName Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);
        Assert.Contains(diagnostics, d => d.Id == "NOF023");
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // Should NOT auto-unwrap and rewrap; should fall back to mapper
        Assert.Contains("mapper.Map<", code);
        Assert.DoesNotContain("DisplayName.Of(", code);
    }

    // =======================================================================
    //  Cross-wrapper tests
    // =======================================================================

    // -----------------------------------------------------------------------
    // Optional<IValueObject<string>> 閳?string : Optional unwrap is to non-nullable 閳?NOF022
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalOfValueObject_ToNonNullable_EmitsNOF022()
    {
        var source = $$"""
            #nullable enable
            using NOF.Application;
            using NOF.Contract;
            using NOF.Domain;
            namespace Test
            {
                {{ValueObjectDefs}}
                public class Order { public Optional<OrderName> Name { get; set; } }
                public class OrderDto { public string Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);
        // Optional<OrderName> 閳?string (non-nullable) is a semantic mismatch
        Assert.Contains(diagnostics, d => d.Id == "NOF022");
    }

    // -----------------------------------------------------------------------
    // Optional<IValueObject<string>> 閳?string? : valid 閳?unwrap Optional, then unwrap VO
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalOfValueObject_ToNullable_UnwrapsBoth()
    {
        var source = $$"""
            #nullable enable
            using NOF.Application;
            using NOF.Contract;
            using NOF.Domain;
            namespace Test
            {
                {{ValueObjectDefs}}
                public class Order { public Optional<OrderName> Name { get; set; } }
                public class OrderDto { public string? Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // Should unwrap Optional via .Value, yielding OrderName, then unwrap VO via explicit cast
        Assert.Contains("src.Name.Value", code);
        Assert.Contains("(string)", code);
    }

    // -----------------------------------------------------------------------
    // string 閳?Optional<IValueObject<string>>  (nested conversion 閳?mapper fallback)
    // -----------------------------------------------------------------------

    [Fact]
    public void StringToOptionalOfValueObject_FallsBackToMapper()
    {
        var source = $$"""
            using NOF.Application;
            using NOF.Contract;
            using NOF.Domain;
            namespace Test
            {
                {{ValueObjectDefs}}
                public class Order { public string Name { get; set; } }
                public class OrderDto { public Optional<OrderName> Name { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // Nested conversion (string 閳?OrderName 閳?Optional<OrderName>) is too complex;
        // no implicit conversion from string to Optional<OrderName> 閳?falls back to mapper
        Assert.Contains("mapper.Map<", code);
    }

    // -----------------------------------------------------------------------
    // TwoWay: ValueObject property (domain 閳?DTO round-trip)
    // -----------------------------------------------------------------------

    [Fact]
    public void TwoWay_ValueObject_GeneratesBothDirections()
    {
        var source = $$"""
            using NOF.Application;
            using NOF.Domain;
            namespace Test
            {
                {{ValueObjectDefs}}
                public class Order { public OrderName Name { get; set; } }
                public class OrderDto { public string Name { get; set; } }

                [Mappable<Order, OrderDto>(TwoWay = true)]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // Forward: OrderName 閳?string (unwrap)
        Assert.Contains("MapperRegistry.Register<global::Test.Order, global::Test.OrderDto>", code);
        Assert.Contains("(string)src.Name)", code);
        // Reverse: string 閳?OrderName (wrap)
        Assert.Contains("MapperRegistry.Register<global::Test.OrderDto, global::Test.Order>", code);
        Assert.Contains("OrderName.Of(src.Name)", code);
    }

    // -----------------------------------------------------------------------
    // NOF023: mapper fallback to unregistered mapping emits warning
    // -----------------------------------------------------------------------

    [Fact]
    public void MapperFallback_UnregisteredPair_EmitsNOF023()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public class Nested { public int X { get; set; } }
                public class Order { public Nested Data { get; set; } }
                public class NestedDto { public int X { get; set; } }
                public class OrderDto { public NestedDto Data { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);
        Assert.Contains(diagnostics, d => d.Id == "NOF023");
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("mapper.Map<", code);
    }

    // -----------------------------------------------------------------------
    // No NOF023 when mapper fallback pair IS auto-generated
    // -----------------------------------------------------------------------

    [Fact]
    public void MapperFallback_RegisteredPair_NoNOF023()
    {
        const string source = """
            using NOF.Application;
            namespace Test
            {
                public class Nested { public int X { get; set; } }
                public class Order { public Nested Data { get; set; } }
                public class NestedDto { public int X { get; set; } }
                public class OrderDto { public NestedDto Data { get; set; } }

                [Mappable<Nested, NestedDto>]
                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var (result, diagnostics) = RunGeneratorWithDiagnostics(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "NOF023");
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains("mapper.Map<", code);
    }

    // =======================================================================
    //  Nullable<ValueObject> tests
    // =======================================================================

    // -----------------------------------------------------------------------
    // Nullable<VO> 閳?T?  (e.g. ConfigNodeId? 閳?long?)
    // -----------------------------------------------------------------------

    [Fact]
    public void NullableValueObject_ToNullablePrimitive_UnwrapsWithHasValue()
    {
        const string source = """
            using NOF.Application;
            using NOF.Domain;
            namespace Test
            {
                public readonly struct NodeId : IValueObject<long>
                {
                    private readonly long _value;
                    private NodeId(long value) { _value = value; }
                    public static NodeId Of(long value) => new(value);
                    public static explicit operator long(NodeId vo) => vo._value;
                    public long GetUnderlyingValue() => _value;
                }

                public class Order { public NodeId? ParentId { get; set; } }
                public class OrderDto { public long? ParentId { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // Should unwrap Nullable<NodeId> via .HasValue / .Value, then explicit cast to long
        Assert.Contains(".HasValue", code);
        Assert.Contains("(long)", code);
    }

    // -----------------------------------------------------------------------
    // T? 閳?Nullable<VO>  (e.g. long? 閳?ConfigNodeId?)
    // -----------------------------------------------------------------------

    [Fact]
    public void NullablePrimitive_ToNullableValueObject_WrapsWithOf()
    {
        const string source = """
            using NOF.Application;
            using NOF.Domain;
            namespace Test
            {
                public readonly struct NodeId : IValueObject<long>
                {
                    private readonly long _value;
                    private NodeId(long value) { _value = value; }
                    public static NodeId Of(long value) => new(value);
                    public static explicit operator long(NodeId vo) => vo._value;
                    public long GetUnderlyingValue() => _value;
                }

                public class Order { public long? ParentId { get; set; } }
                public class OrderDto { public NodeId? ParentId { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // Should unwrap Nullable<long>, then wrap via NodeId.Of
        Assert.Contains(".HasValue", code);
        Assert.Contains("NodeId.Of(", code);
    }

    // =======================================================================
    //  IEnumerable<T> / List<T> / array mapping tests
    // =======================================================================

    // -----------------------------------------------------------------------
    // List<T> 閳?List<U>  (recursive element conversion)
    // -----------------------------------------------------------------------

    [Fact]
    public void ListToList_RecursiveElementConversion()
    {
        const string source = """
            using System.Collections.Generic;
            using NOF.Application;
            namespace Test
            {
                public class Inner { public int X { get; set; } }
                public class InnerDto { public int X { get; set; } }
                public class Order { public List<Inner> Items { get; set; } }
                public class OrderDto { public List<InnerDto> Items { get; set; } }

                [Mappable<Inner, InnerDto>]
                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // Should emit [..src.Items.Select(...)] collection expression
        Assert.Contains(".Select(", code);
        Assert.Contains("[..", code);
    }

    // -----------------------------------------------------------------------
    // IReadOnlyList<T> 閳?List<U>  (different collection types)
    // -----------------------------------------------------------------------

    [Fact]
    public void IReadOnlyListToList_RecursiveElementConversion()
    {
        const string source = """
            using System.Collections.Generic;
            using NOF.Application;
            namespace Test
            {
                public class Inner { public int X { get; set; } }
                public class InnerDto { public int X { get; set; } }
                public class Order { public IReadOnlyList<Inner> Items { get; set; } }
                public class OrderDto { public List<InnerDto> Items { get; set; } }

                [Mappable<Inner, InnerDto>]
                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        Assert.Contains(".Select(", code);
        Assert.Contains("[..", code);
    }

    // -----------------------------------------------------------------------
    // List<VO> 閳?List<T>  (element-level VO unwrap)
    // -----------------------------------------------------------------------

    [Fact]
    public void ListOfValueObject_ToListOfPrimitive_UnwrapsElements()
    {
        var source = $$"""
            using System.Collections.Generic;
            using NOF.Application;
            using NOF.Domain;
            namespace Test
            {
                {{ValueObjectDefs}}
                public class Order { public List<OrderName> Names { get; set; } }
                public class OrderDto { public List<string> Names { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // Should emit [..Select with explicit cast] collection expression
        Assert.Contains(".Select(", code);
        Assert.Contains("(string)", code);
        Assert.Contains("[..", code);
    }

    // -----------------------------------------------------------------------
    // List<T> 閳?List<T>  (same element type, no conversion)
    // -----------------------------------------------------------------------

    [Fact]
    public void ListToList_SameElementType_DirectAssignment()
    {
        const string source = """
            using System.Collections.Generic;
            using NOF.Application;
            namespace Test
            {
                public class Order { public List<int> Scores { get; set; } }
                public class OrderDto { public List<int> Scores { get; set; } }

                [Mappable<Order, OrderDto>]
                public static partial class Mappings;
            }
            """;

        var result = RunGenerator(source);
        var code = result.GeneratedTrees.Single().GetText().ToString();
        // Same element type 閳?no .Select needed, just direct assignment
        Assert.Contains("Scores = src.Scores", code);
        Assert.DoesNotContain(".Select(", code);
    }
}

