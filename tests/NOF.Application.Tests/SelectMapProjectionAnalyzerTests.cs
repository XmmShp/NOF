using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NOF.Application.SourceGenerator;
using System.Collections.Immutable;
using Xunit;

namespace NOF.Application.Tests;

public sealed class SelectMapProjectionAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = CSharpCompilation.CreateCompilation(
            "TestAssembly",
            source,
            isDll: true,
            typeof(IMapper),
            typeof(Queryable));
        var compilationErrors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(compilationErrors);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new SelectMapProjectionAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task QueryableSelectCallingMapperMap_ReportsNOF026()
    {
        const string source = """
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IQueryable<OrderDto> Run(IQueryable<Order> orders, IMapper mapper)
                    => orders.Select(x => mapper.Map<Order, OrderDto>(x));
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF026");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("ProjectTo<OrderDto>(mapper)", diagnostic.GetMessage());
    }

    [Fact]
    public async Task StaticQueryableSelectCallingMapperMap_ReportsNOF026()
    {
        const string source = """
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IQueryable<OrderDto> Run(IQueryable<Order> orders, IMapper mapper)
                    => Queryable.Select(orders, x => mapper.Map<Order, OrderDto>(x));
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF026");
    }

    [Fact]
    public async Task EnumerableSelectCallingMapperMap_DoesNotReportNOF026()
    {
        const string source = """
            using NOF.Application;
            using System.Collections.Generic;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IEnumerable<OrderDto> Run(IEnumerable<Order> orders, IMapper mapper)
                    => orders.Select(x => mapper.Map<Order, OrderDto>(x));
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF026");
    }

    [Fact]
    public async Task SelectMappingTransformedValue_DoesNotReportNOF026()
    {
        const string source = """
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IQueryable<OrderDto> Run(IQueryable<Order> orders, IMapper mapper)
                    => orders.Select(x => mapper.Map<Order, OrderDto>(new Order(x.Id + 1)));
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF026");
    }

    [Fact]
    public async Task SelectUsingNamedMapping_DoesNotReportNOF026()
    {
        const string source = """
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IQueryable<OrderDto> Run(IQueryable<Order> orders, IMapper mapper)
                    => orders.Select(x => mapper.Map<Order, OrderDto>(x, "summary"));
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF026");
    }

    [Fact]
    public async Task UnrelatedMapMethod_DoesNotReportNOF026()
    {
        const string source = """
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);
            public sealed class OtherMapper
            {
                public TDestination Map<TSource, TDestination>(TSource source) => default!;
            }

            public static class Queries
            {
                public static IQueryable<OrderDto> Run(IQueryable<Order> orders, OtherMapper mapper)
                    => orders.Select(x => mapper.Map<Order, OrderDto>(x));
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF026");
    }
}
