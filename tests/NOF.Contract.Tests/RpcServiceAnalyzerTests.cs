using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NOF.Contract;
using NOF.Contract.SourceGenerator;
using NOF.SourceGenerator.Tests.Extensions;
using System.Collections.Immutable;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class RpcServiceAnalyzerTests
{
    private static readonly Type[] _refs =
    [
        typeof(HttpEndpointAttribute),
        typeof(IRpcService),
        typeof(HttpVerb),
        typeof(Result),
        typeof(Result<>)
    ];

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var extraReferences = _refs.Select(t => t.ToMetadataReference()).ToArray();
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new RpcServiceAnalyzer());
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
        Assert.Contains(diagnostics, d => d.Id == "NOF200");
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
        Assert.Single(diagnostics, d => d.Id == "NOF201");
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
        Assert.Contains(diagnostics, d => d.Id == "NOF202");
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

            
            public partial interface IMyService : IRpcService
            {
                Task<Result> DoAsync(Query1 first, Query2 second);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Single(diagnostics, d => d.Id == "NOF207");
    }

    [Fact]
    public async Task ServiceMethod_WithoutRequestParameter_ReportsNOF207()
    {
        const string source = """
            using NOF.Contract;
            using System.Threading.Tasks;

            namespace App;

            
            public partial interface IMyService : IRpcService
            {
                Task<Result> DoAsync();
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Single(diagnostics, d => d.Id == "NOF207");
    }

    [Fact]
    public async Task ServiceMethod_WithCancellationToken_ReportsNOF207()
    {
        const string source = """
            using NOF.Contract;
            using System.Threading;
            using System.Threading.Tasks;

            namespace App;

            public record Query(string Value);

            
            public partial interface IMyService : IRpcService
            {
                Task<Result> DoAsync(Query request, CancellationToken cancellationToken = default);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Single(diagnostics, d => d.Id == "NOF207");
    }

    [Fact]
    public async Task ServiceMethod_WithSyncReturnAndSingleRequest_NoNOF207()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record Query(string Value);

            
            public partial interface IMyService : IRpcService
            {
                Result Do(Query request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "NOF207");
    }

    [Fact]
    public async Task ServiceMethod_WithValueTaskReturn_ReportsNOF207()
    {
        const string source = """
            using NOF.Contract;
            using System.Threading.Tasks;

            namespace App;

            public record Query(string Value);

            
            public partial interface IMyService : IRpcService
            {
                ValueTask<Result> DoAsync(Query request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Single(diagnostics, d => d.Id == "NOF207");
    }

    [Fact]
    public async Task ServiceMethod_WithTaskAndSingleRequest_ReportsNOF207()
    {
        const string source = """
            using NOF.Contract;
            using System.Threading.Tasks;

            namespace App;

            public record Query(string Value);

            
            public partial interface IMyService : IRpcService
            {
                Task<Result<string>> DoAsync(Query request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Single(diagnostics, d => d.Id == "NOF207");
    }
}
