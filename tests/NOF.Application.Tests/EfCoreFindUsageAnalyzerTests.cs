using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.EntityFrameworkCore;
using NOF.Application;
using NOF.Application.SourceGenerator;
using System.Collections.Immutable;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public sealed class FindUsageAnalyzerTests
{
    private static readonly Type[] _refs =
    [
        typeof(IDbSet<>),
        typeof(DbContext),
        typeof(EntityFrameworkQueryableExtensions)
    ];

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var extraReferences = _refs.Select(t => t.ToMetadataReference()).ToArray();
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new FindUsageAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task DbContextFindAsync_ReportsNOF302()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Test;

            public sealed class Order
            {
                public int Id { get; set; }
            }

            public sealed class OrderHandler
            {
                private readonly DbContext _dbContext;

                public OrderHandler(DbContext dbContext)
                {
                    _dbContext = dbContext;
                }

                public async Task<Order?> GetAsync(CancellationToken cancellationToken)
                {
                    return await _dbContext.FindAsync<Order>([1], cancellationToken);
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Contains(diagnostics, d => d.Id == "NOF302" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task DbSetFindAsync_ReportsNOF302()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Test;

            public sealed class Order
            {
                public int Id { get; set; }
            }

            public sealed class OrderHandler
            {
                private readonly DbContext _dbContext;

                public OrderHandler(DbContext dbContext)
                {
                    _dbContext = dbContext;
                }

                public async Task<Order?> GetAsync(CancellationToken cancellationToken)
                {
                    return await _dbContext.Set<Order>().FindAsync([1], cancellationToken);
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Contains(diagnostics, d => d.Id == "NOF302" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task DbContextFind_ReportsNOF303()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;

            namespace Test;

            public sealed class Order
            {
                public int Id { get; set; }
            }

            public sealed class OrderHandler
            {
                private readonly DbContext _dbContext;

                public OrderHandler(DbContext dbContext)
                {
                    _dbContext = dbContext;
                }

                public Order? Get()
                {
                    return _dbContext.Find<Order>(1);
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Contains(diagnostics, d => d.Id == "NOF303" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_DoesNotReportNOF302()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Test;

            public sealed class Order
            {
                public int Id { get; set; }
            }

            public sealed class OrderHandler
            {
                private readonly DbContext _dbContext;

                public OrderHandler(DbContext dbContext)
                {
                    _dbContext = dbContext;
                }

                public Task<Order?> GetAsync(CancellationToken cancellationToken)
                {
                    return _dbContext.Set<Order>().FirstOrDefaultAsync(order => order.Id == 1, cancellationToken);
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "NOF302");
    }

    [Fact]
    public async Task FirstOrDefault_DoesNotReportNOF303()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using System.Linq;

            namespace Test;

            public sealed class Order
            {
                public int Id { get; set; }
            }

            public sealed class OrderHandler
            {
                private readonly DbContext _dbContext;

                public OrderHandler(DbContext dbContext)
                {
                    _dbContext = dbContext;
                }

                public Order? Get()
                {
                    return _dbContext.Set<Order>().FirstOrDefault(order => order.Id == 1);
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "NOF303");
    }

    [Fact]
    public async Task FirstOrDefaultAsync_OnApplicationAbstraction_DoesNotReportNOF302()
    {
        const string source = """
            using NOF.Application;
            using NOF.Application.Data;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Test;

            public sealed class Order
            {
                public int Id { get; set; }
            }

            public sealed class OrderHandler
            {
                private readonly IDbContext _dbContext;

                public OrderHandler(IDbContext dbContext)
                {
                    _dbContext = dbContext;
                }

                public Task<Order?> GetAsync(CancellationToken cancellationToken)
                {
                    return _dbContext.Set<Order>().FirstOrDefaultAsync(order => order.Id == 1, cancellationToken);
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "NOF302");
    }
}
