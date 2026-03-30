using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NOF.Contract;
using NOF.Contract.SourceGenerator;
using NOF.SourceGenerator.Tests.Extensions;
using System.Collections.Immutable;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class ExposeToHttpEndpointAnalyzerTests
{
    private static readonly Type[] _refs =
    [
        typeof(HttpEndpointAttribute),
        typeof(GenerateServiceAttribute),
        typeof(HttpVerb),
        typeof(Result),
        typeof(Result<>)
    ];

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var extraReferences = _refs.Select(t => t.ToMetadataReference()).ToArray();
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ExposeToHttpEndpointAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task StructRequest_WithHttpEndpoint_ReportsNOF200()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [HttpEndpoint(HttpVerb.Post, "/api/items")]
                public struct CreateItemRequest
                {
                    public string Name { get; set; }
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        diagnostics.Should().Contain(d => d.Id == "NOF200");
    }

    [Fact]
    public async Task MissingRouteParamProperty_ReportsNOF201()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [HttpEndpoint(HttpVerb.Put, "/api/items/{id}")]
                public class UpdateItemRequest
                {
                    public string Name { get; set; } = default!;
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        diagnostics.Should().ContainSingle(d => d.Id == "NOF201");
    }

    [Fact]
    public async Task ClassWithoutParameterlessCtor_ReportsNOF202()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [HttpEndpoint(HttpVerb.Post, "/api/items")]
                public class CreateItemRequest
                {
                    public CreateItemRequest(string name) { Name = name; }
                    public string Name { get; set; }
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        diagnostics.Should().Contain(d => d.Id == "NOF202");
    }

    [Fact]
    public async Task ServiceMethod_WithTwoBusinessParameters_ReportsNOF207()
    {
        const string source = """
            using NOF.Contract;
            using System.Threading.Tasks;

            namespace App;

            public record Query1(string Value);
            public record Query2(string Value);

            [GenerateService]
            public partial interface IMyService
            {
                Task<Result> DoAsync(Query1 first, Query2 second);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        diagnostics.Should().ContainSingle(d => d.Id == "NOF207");
    }

    [Fact]
    public async Task ServiceMethod_WithSyncReturn_ReportsNOF207()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record Query(string Value);

            [GenerateService]
            public partial interface IMyService
            {
                Result Do(Query request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        diagnostics.Should().ContainSingle(d => d.Id == "NOF207");
    }

    [Fact]
    public async Task ServiceMethod_WithValueTaskReturn_ReportsNOF207()
    {
        const string source = """
            using NOF.Contract;
            using System.Threading.Tasks;

            namespace App;

            public record Query(string Value);

            [GenerateService]
            public partial interface IMyService
            {
                ValueTask<Result> DoAsync(Query request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        diagnostics.Should().ContainSingle(d => d.Id == "NOF207");
    }

    [Fact]
    public async Task ServiceMethod_WithTaskAndSingleRequest_NoNOF207()
    {
        const string source = """
            using NOF.Contract;
            using System.Threading;
            using System.Threading.Tasks;

            namespace App;

            public record Query(string Value);

            [GenerateService]
            public partial interface IMyService
            {
                Task<Result<string>> DoAsync(Query request, CancellationToken cancellationToken = default);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        diagnostics.Should().NotContain(d => d.Id == "NOF207");
    }
}
