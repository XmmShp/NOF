using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.EntityFrameworkCore;
using NOF.Application.SourceGenerator;
using System.Collections.Immutable;
using Xunit;

namespace NOF.Application.Tests;

public sealed class ProjectToOrderingAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = CSharpCompilation.CreateCompilation(
            "TestAssembly",
            source,
            isDll: true,
            typeof(IMapper),
            typeof(Queryable),
            typeof(EntityFrameworkQueryableExtensions));
        var compilationErrors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(compilationErrors);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ProjectToOrderingAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    [Theory]
    [InlineData(".Where(dto => dto.Id > 0)", "Where")]
    [InlineData(".OrderBy(dto => dto.Id)", "OrderBy")]
    [InlineData(".Skip(1)", "Skip")]
    [InlineData(".Take(1)", "Take")]
    [InlineData(".Select(dto => dto.Id)", "Select")]
    public async Task QueryShapingAfterProjection_ReportsNOF025(string suffix, string operation)
    {
        var source = $$"""
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IQueryable Run(IQueryable<Order> orders)
                    => orders.ProjectTo<OrderDto>(){{suffix}};
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains(operation, diagnostic.GetMessage());
    }

    [Fact]
    public async Task PredicateTerminalAfterProjection_ReportsNOF025()
    {
        const string source = """
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static OrderDto? Run(IQueryable<Order> orders)
                    => orders.ProjectTo<OrderDto>().FirstOrDefault(dto => dto.Id > 0);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
        Assert.Contains("FirstOrDefault", diagnostic.GetMessage());
    }

    [Fact]
    public async Task EfAsyncPredicateTerminalAfterProjection_ReportsNOF025()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using NOF.Application;
            using System.Linq;
            using System.Threading.Tasks;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static Task<bool> Run(IQueryable<Order> orders)
                    => EntityFrameworkQueryableExtensions.AnyAsync(
                        orders.ProjectTo<OrderDto>(),
                        dto => dto.Id > 0);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
        Assert.Contains("AnyAsync", diagnostic.GetMessage());
    }

    [Fact]
    public async Task LocalVariableAndAliasAfterProjection_ReportNOF025()
    {
        const string source = """
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IQueryable<OrderDto> Run(IQueryable<Order> orders)
                {
                    var projected = orders.ProjectTo<OrderDto>();
                    var alias = projected;
                    return alias.Where(dto => dto.Id > 0);
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
    }

    [Fact]
    public async Task StaticQueryableCallAfterProjection_ReportsNOF025()
    {
        const string source = """
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IQueryable<OrderDto> Run(IQueryable<Order> orders)
                    => Queryable.Where(orders.ProjectTo<OrderDto>(), dto => dto.Id > 0);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
    }

    [Fact]
    public async Task ProjectionUsedAsSecondSetOperand_ReportsNOF025()
    {
        const string source = """
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IQueryable<OrderDto> Run(
                    IQueryable<Order> orders,
                    IQueryable<OrderDto> other)
                    => other.Concat(orders.ProjectTo<OrderDto>());
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
    }

    [Fact]
    public async Task TransparentAsQueryableBeforeShaping_StillReportsNOF025Once()
    {
        const string source = """
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IQueryable<OrderDto> Run(IQueryable<Order> orders)
                    => orders.ProjectTo<OrderDto>().AsQueryable().Where(dto => dto.Id > 0);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
        Assert.Contains("Where", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ExplicitAndNamedProjectionOverload_ReportsNOF025()
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
                    => orders.ProjectTo<OrderDto>(mapper, "summary").Where(dto => dto.Id > 0);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
    }

    [Theory]
    [InlineData(".ToList()")]
    [InlineData(".ToArray()")]
    [InlineData(".FirstOrDefault()")]
    [InlineData(".Any()")]
    [InlineData(".Count()")]
    [InlineData(".AsEnumerable().Where(dto => dto.Id > 0)")]
    public async Task MaterializationOrClientSideWork_DoesNotReportNOF025(string suffix)
    {
        var source = $$"""
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static object? Run(IQueryable<Order> orders)
                    => orders.ProjectTo<OrderDto>(){{suffix}};
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
    }

    [Fact]
    public async Task PassingOrReturningProjectedQuery_DoesNotReportNOF025()
    {
        const string source = """
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IQueryable<OrderDto> Return(IQueryable<Order> orders)
                    => orders.ProjectTo<OrderDto>();

                public static object Pass(IQueryable<Order> orders)
                    => Consume(orders.ProjectTo<OrderDto>());

                private static object Consume(IQueryable<OrderDto> query) => query;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
    }

    [Fact]
    public async Task EfAsyncMaterialization_DoesNotReportNOF025()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using NOF.Application;
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading.Tasks;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static Task<List<OrderDto>> Run(IQueryable<Order> orders)
                    => EntityFrameworkQueryableExtensions.ToListAsync(
                        orders.ProjectTo<OrderDto>());
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
    }

    [Fact]
    public async Task SelfReassignmentAfterProjection_ReportsNOF025()
    {
        const string source = """
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IQueryable<OrderDto> Run(IQueryable<Order> orders)
                {
                    var projected = orders.ProjectTo<OrderDto>();
                    projected = projected.Where(dto => dto.Id > 0);
                    return projected;
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
    }

    [Fact]
    public async Task ReassignedLocal_DoesNotReportBecauseOriginIsAmbiguous()
    {
        const string source = """
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IQueryable<OrderDto> Run(
                    IQueryable<Order> orders,
                    IQueryable<OrderDto> replacement)
                {
                    var projected = orders.ProjectTo<OrderDto>();
                    projected = replacement;
                    return projected.Where(dto => dto.Id > 0);
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
    }

    [Fact]
    public async Task ConditionalOrigin_DoesNotReportBecauseNotEveryPathIsProjected()
    {
        const string source = """
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IQueryable<OrderDto> Run(
                    IQueryable<Order> orders,
                    IQueryable<OrderDto> replacement,
                    bool useProjection)
                {
                    var query = useProjection
                        ? orders.ProjectTo<OrderDto>()
                        : replacement;
                    return query.Where(dto => dto.Id > 0);
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
    }

    [Fact]
    public async Task QueryTagAfterProjection_DoesNotReportNOF025()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using NOF.Application;
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class Queries
            {
                public static IQueryable<OrderDto> Run(IQueryable<Order> orders)
                    => orders.ProjectTo<OrderDto>().TagWith("projected orders");
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
    }

    [Fact]
    public async Task UnrelatedProjectToMethod_DoesNotReportNOF025()
    {
        const string source = """
            using System.Linq;

            namespace Test;

            public sealed record Order(int Id);
            public sealed record OrderDto(int Id);

            public static class OtherProjection
            {
                public static IQueryable<OrderDto> ProjectTo(IQueryable<Order> source)
                    => source.Select(order => new OrderDto(order.Id));
            }

            public static class Queries
            {
                public static IQueryable<OrderDto> Run(IQueryable<Order> orders)
                    => OtherProjection.ProjectTo(orders).Where(dto => dto.Id > 0);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF025");
    }
}
