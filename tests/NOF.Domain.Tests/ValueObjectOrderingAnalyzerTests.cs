using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NOF.Domain;
using NOF.Domain.SourceGenerator;
using System.Collections.Immutable;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public sealed class ValueObjectOrderingAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = CSharpCompilation.CreateCompilation(
            "TestAssembly",
            source,
            isDll: true,
            typeof(IValueObject<>),
            typeof(Enumerable),
            typeof(Queryable));
        GeneratorDriver generatorDriver = CSharpGeneratorDriver.Create(new ValueObjectGenerator());
        generatorDriver.RunGeneratorsAndUpdateCompilation(compilation, out var generatedCompilation, out _);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ValueObjectOrderingAnalyzer());
        return await generatedCompilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    [Theory]
    [InlineData("OrderBy")]
    [InlineData("OrderByDescending")]
    public async Task EnumerableOrdering_WithValueObjectKey_ReportsNOF015(string methodName)
    {
        var source = $$"""
            using NOF.Domain;
            using System.Collections.Generic;
            using System.Linq;

            namespace Test;

            public readonly partial struct OrderId : IValueObject<long>;
            public static class Queries
            {
                public static IEnumerable<OrderId> Sort(IEnumerable<OrderId> ids)
                    => ids.{{methodName}}(id => id);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF015");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("OrderId", diagnostic.GetMessage());
        Assert.Contains("long", diagnostic.GetMessage());
    }

    [Fact]
    public async Task QueryableThenBy_WithValueObjectKey_ReportsNOF015()
    {
        const string source = """
            using NOF.Domain;
            using System.Linq;

            namespace Test;

            public readonly partial struct OrderId : IValueObject<long>;
            public sealed record Order(string Name, OrderId Id);

            public static class Queries
            {
                public static IQueryable<Order> Sort(IQueryable<Order> orders)
                    => orders.OrderBy(order => order.Name).ThenBy(order => order.Id);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "NOF015");
    }

    [Fact]
    public async Task Ordering_WithPrimitiveCast_DoesNotReportNOF015()
    {
        const string source = """
            using NOF.Domain;
            using System.Collections.Generic;
            using System.Linq;

            namespace Test;

            public readonly partial struct OrderId : IValueObject<long>;
            public sealed record Order(OrderId Id);

            public static class Queries
            {
                public static IEnumerable<Order> Sort(IEnumerable<Order> orders)
                    => orders.OrderBy(order => (long)order.Id);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF015");
    }

    [Fact]
    public async Task Ordering_WithNullableValueObjectKey_ReportsNOF015()
    {
        const string source = """
            using NOF.Domain;
            using System.Collections.Generic;
            using System.Linq;

            namespace Test;

            public readonly partial struct OrderId : IValueObject<long>;
            public sealed record Order(OrderId? Id);

            public static class Queries
            {
                public static IEnumerable<Order> Sort(IEnumerable<Order> orders)
                    => orders.OrderBy(order => order.Id);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "NOF015");
    }

    [Fact]
    public async Task Ordering_WithGenericComparableValueObject_DoesNotReportNOF015()
    {
        const string source = """
            using NOF.Domain;
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Test;

            public readonly partial struct OrderId : IValueObject<long>, IComparable<OrderId>
            {
                public int CompareTo(OrderId other) => ((long)this).CompareTo((long)other);
            }

            public static class Queries
            {
                public static IEnumerable<OrderId> Sort(IEnumerable<OrderId> ids)
                    => ids.OrderBy(id => id);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF015");
    }

    [Fact]
    public async Task Ordering_WithNonGenericComparableValueObject_DoesNotReportNOF015()
    {
        const string source = """
            using NOF.Domain;
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Test;

            public readonly partial struct OrderId : IValueObject<long>, IComparable
            {
                public int CompareTo(object? other)
                    => other is OrderId id ? ((long)this).CompareTo((long)id) : 1;
            }

            public static class Queries
            {
                public static IEnumerable<OrderId> Sort(IEnumerable<OrderId> ids)
                    => ids.OrderBy(id => id);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF015");
    }

    [Fact]
    public async Task UnrelatedOrderByMethod_DoesNotReportNOF015()
    {
        const string source = """
            using NOF.Domain;

            namespace Test;

            public readonly partial struct OrderId : IValueObject<long>;

            public static class CustomOrdering
            {
                public static OrderId OrderBy(OrderId id, System.Func<OrderId, OrderId> selector)
                    => selector(id);

                public static OrderId Sort(OrderId id)
                    => OrderBy(id, value => value);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF015");
    }
}
