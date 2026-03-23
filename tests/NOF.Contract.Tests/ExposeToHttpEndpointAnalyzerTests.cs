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
        typeof(PublicApiAttribute),
        typeof(HttpEndpointAttribute),
        typeof(GenerateServiceAttribute),
        typeof(HttpVerb),
        typeof(IRequest),
        typeof(IRequest<>),
        typeof(IRequestSender),
        typeof(Result),
        typeof(Result<>)
    ];

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var extraReferences = _refs.Select(t => t.ToMetadataReference()).ToArray();
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ExposeToHttpEndpointAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        return diagnostics;
    }

    // --- HttpEndpoint + PublicApi validation ---

    [Fact]
    public async Task StructRequest_WithPublicApi_ReportsError()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [PublicApi]
                [HttpEndpoint(HttpVerb.Post, "/api/items")]
                public struct CreateItemRequest : IRequest
                {
                    public string Name { get; set; }
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().Contain(d => d.Id == "NOF200");
        diagnostics.First(d => d.Id == "NOF200").GetMessage().Should().Contain("CreateItemRequest");
    }

    [Fact]
    public async Task HttpEndpointWithoutPublicApi_ReportsError()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [HttpEndpoint(HttpVerb.Post, "/api/items")]
                public record CreateItemRequest(string Name) : IRequest;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(d => d.Id == "NOF204");
        diagnostics.First(d => d.Id == "NOF204").GetMessage().Should().Contain("CreateItemRequest");
    }

    [Fact]
    public async Task MissingRouteParamProperty_ReportsError()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [PublicApi]
                [HttpEndpoint(HttpVerb.Put, "/api/items/{id}")]
                public class UpdateItemRequest(long id) : IRequest
                {
                    public string Name { get; set; } = default!;
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(d => d.Id == "NOF201");
        var diag = diagnostics.First(d => d.Id == "NOF201");
        diag.GetMessage().Should().Contain("UpdateItemRequest").And.Contain("id");
    }

    [Fact]
    public async Task ClassWithPrimaryCtor_NoParameterlessCtor_ReportsError()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [PublicApi]
                [HttpEndpoint(HttpVerb.Put, "/api/items/{id}")]
                public class UpdateItemRequest(long id) : IRequest
                {
                    public string Name { get; set; } = default!;
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().Contain(d => d.Id == "NOF202");
        diagnostics.First(d => d.Id == "NOF202").GetMessage().Should().Contain("UpdateItemRequest");
    }

    [Fact]
    public async Task ClassWithExplicitParameterlessCtor_NoDiagnostic()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [PublicApi]
                [HttpEndpoint(HttpVerb.Put, "/api/items/{id}")]
                public class UpdateItemRequest : IRequest
                {
                    public long Id { get; set; }
                    public string Name { get; set; } = default!;
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().NotContain(d => d.Id == "NOF200");
        diagnostics.Should().NotContain(d => d.Id == "NOF201");
        diagnostics.Should().NotContain(d => d.Id == "NOF202");
    }

    [Fact]
    public async Task RecordWithPrimaryCtor_NoDiagnostic()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [PublicApi]
                [HttpEndpoint(HttpVerb.Put, "/api/items/{id}")]
                public record UpdateItemRequest(long Id) : IRequest
                {
                    public string? Value { get; set; }
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().NotContain(d => d.Id == "NOF200");
        diagnostics.Should().NotContain(d => d.Id == "NOF201");
        diagnostics.Should().NotContain(d => d.Id == "NOF202");
    }

    [Fact]
    public async Task RecordWithAllPropsInCtor_NoDiagnostic()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [PublicApi]
                [HttpEndpoint(HttpVerb.Put, "/api/nodes/{nodeId}/files/{fileName}")]
                public record AddFileRequest(long NodeId, string FileName, string Content) : IRequest;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().NotContain(d => d.Id == "NOF200");
        diagnostics.Should().NotContain(d => d.Id == "NOF201");
        diagnostics.Should().NotContain(d => d.Id == "NOF202");
    }

    [Fact]
    public async Task MultipleRouteParams_OneMissing_ReportsErrorForMissingOnly()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [PublicApi]
                [HttpEndpoint(HttpVerb.Put, "/api/nodes/{nodeId}/files/{fileName}")]
                public class AddFileRequest : IRequest
                {
                    public long NodeId { get; set; }
                    public string Content { get; set; } = default!;
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(d => d.Id == "NOF201");
        diagnostics.First(d => d.Id == "NOF201").GetMessage().Should().Contain("fileName");
    }

    [Fact]
    public async Task NoRouteParams_ClassWithoutParameterlessCtor_StillReportsCtorError()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [PublicApi]
                [HttpEndpoint(HttpVerb.Post, "/api/items")]
                public class CreateItemRequest : IRequest
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
    public async Task ClassWithBothParameterlessAndParameterizedCtor_NoDiagnostic()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [PublicApi]
                [HttpEndpoint(HttpVerb.Post, "/api/items")]
                public class CreateItemRequest : IRequest
                {
                    public CreateItemRequest() { }
                    public CreateItemRequest(string name) { Name = name; }
                    public string Name { get; set; } = default!;
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().NotContain(d => d.Id == "NOF202");
    }

    [Fact]
    public async Task RouteParamMatchIsCaseInsensitive_NoDiagnostic()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [PublicApi]
                [HttpEndpoint(HttpVerb.Delete, "/api/items/{id}")]
                public record DeleteItemRequest(long Id) : IRequest;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().NotContain(d => d.Id == "NOF201");
    }

    // --- PublicApi OperationName validation ---

    [Fact]
    public async Task InvalidOperationName_OnPublicApi_ReportsError()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [PublicApi(OperationName = "create-item")]
                public record CreateItemRequest(string Name) : IRequest;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(d => d.Id == "NOF203");
        diagnostics.First(d => d.Id == "NOF203").GetMessage().Should().Contain("create-item");
    }

    [Fact]
    public async Task ValidOperationName_NoDiagnostic()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [PublicApi(OperationName = "CreateItem")]
                [HttpEndpoint(HttpVerb.Post, "/api/items")]
                public record CreateItemRequest(string Name) : IRequest;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().NotContain(d => d.Id == "NOF203");
    }

    [Fact]
    public async Task NoOperationName_NoDiagnostic()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [PublicApi]
                [HttpEndpoint(HttpVerb.Post, "/api/items")]
                public record CreateItemRequest(string Name) : IRequest;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().NotContain(d => d.Id == "NOF203");
    }

    [Fact]
    public async Task OperationNameWithSpaces_ReportsError()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [PublicApi(OperationName = "Create Item")]
                public record CreateItemRequest(string Name) : IRequest;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(d => d.Id == "NOF203");
    }
}
