using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction.SourceGenerator;
using System.Collections.Immutable;
using Xunit;

namespace NOF.Abstraction.Tests;

public sealed class DaemonServiceResolutionAnalyzerTests
{
    private static readonly Type[] _refs =
    [
        typeof(IDaemonService),
        typeof(AsyncServiceScope),
        typeof(ServiceProviderServiceExtensions),
        typeof(ServiceProviderExtensions)
    ];

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var extraReferences = _refs.Select(type => type.ToMetadataReference()).ToArray();
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new DaemonServiceResolutionAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task CreateScope_WithoutResolveDaemonServices_ReportsNOF040()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using System;

            namespace Test;

            public sealed class Worker
            {
                private readonly IServiceProvider _services;

                public Worker(IServiceProvider services)
                {
                    _services = services;
                }

                public object? Run()
                {
                    using var scope = _services.CreateScope();
                    return scope.ServiceProvider.GetService(typeof(object));
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "NOF040" && diagnostic.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task CreateScope_WithImmediateResolveDaemonServices_DoesNotReportNOF040()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using System;

            namespace Test;

            public sealed class Worker
            {
                private readonly IServiceProvider _services;

                public Worker(IServiceProvider services)
                {
                    _services = services;
                }

                public object? Run()
                {
                    using var scope = _services.CreateScope();
                    var services = scope.ServiceProvider.ResolveDaemonServices();
                    return services.GetService(typeof(object));
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "NOF040");
    }

    [Fact]
    public async Task CreateScope_InUsingStatement_WithImmediateResolveDaemonServices_DoesNotReportNOF040()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using System;

            namespace Test;

            public sealed class Worker
            {
                private readonly IServiceProvider _services;

                public Worker(IServiceProvider services)
                {
                    _services = services;
                }

                public object? Run()
                {
                    using (var scope = _services.CreateScope())
                    {
                        scope.ServiceProvider.ResolveDaemonServices();
                        return scope.ServiceProvider.GetService(typeof(object));
                    }
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "NOF040");
    }
}
