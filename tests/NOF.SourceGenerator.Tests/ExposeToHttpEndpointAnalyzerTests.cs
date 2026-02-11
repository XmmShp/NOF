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
    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true,
            typeof(ExposeToHttpEndpointAttribute).ToMetadataReference());

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ExposeToHttpEndpointAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        return diagnostics;
    }

    [Fact]
    public async Task StructRequest_ReportsError()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [ExposeToHttpEndpoint(HttpVerb.Post, "/api/items")]
                public struct CreateItemRequest : IRequest
                {
                    public string Name { get; set; }
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(d => d.Id == "NOF200");
        diagnostics.First(d => d.Id == "NOF200").GetMessage().Should().Contain("CreateItemRequest");
    }

    [Fact]
    public async Task MissingRouteParamProperty_ReportsError()
    {
        // Class with primary ctor param 'id' — NOT a property, route param {id} has no match
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [ExposeToHttpEndpoint(HttpVerb.Put, "/api/items/{id}")]
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
        // Class with primary ctor — no parameterless ctor
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [ExposeToHttpEndpoint(HttpVerb.Put, "/api/items/{id}")]
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
                [ExposeToHttpEndpoint(HttpVerb.Put, "/api/items/{id}")]
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
        // Record primary ctor params become properties — no errors expected
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [ExposeToHttpEndpoint(HttpVerb.Put, "/api/items/{id}")]
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
                [ExposeToHttpEndpoint(HttpVerb.Put, "/api/nodes/{nodeId}/files/{fileName}")]
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
                [ExposeToHttpEndpoint(HttpVerb.Put, "/api/nodes/{nodeId}/files/{fileName}")]
                public class AddFileRequest : IRequest
                {
                    public long NodeId { get; set; }
                    public string Content { get; set; } = default!;
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        // fileName has no matching property
        diagnostics.Should().ContainSingle(d => d.Id == "NOF201");
        diagnostics.First(d => d.Id == "NOF201").GetMessage().Should().Contain("fileName");
    }

    [Fact]
    public async Task NoRouteParams_ClassWithoutParameterlessCtor_StillReportsCtorError()
    {
        // Even without route params, a class with only a parameterized ctor is invalid
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [ExposeToHttpEndpoint(HttpVerb.Post, "/api/items")]
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
                [ExposeToHttpEndpoint(HttpVerb.Post, "/api/items")]
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
        // Route has {id} but property is Id — should match case-insensitively
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [ExposeToHttpEndpoint(HttpVerb.Delete, "/api/items/{id}")]
                public record DeleteItemRequest(long Id) : IRequest;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().NotContain(d => d.Id == "NOF201");
    }

    [Fact]
    public async Task InvalidOperationName_ReportsError()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [ExposeToHttpEndpoint(HttpVerb.Post, "/api/items", OperationName = "create-item")]
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
                [ExposeToHttpEndpoint(HttpVerb.Post, "/api/items", OperationName = "CreateItem")]
                public record CreateItemRequest(string Name) : IRequest;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().NotContain(d => d.Id == "NOF203");
    }

    [Fact]
    public async Task NoOperationName_NoDiagnostic()
    {
        // When OperationName is not specified, no validation needed
        const string source = """
            using NOF.Contract;
            namespace App
            {
                [ExposeToHttpEndpoint(HttpVerb.Post, "/api/items")]
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
                [ExposeToHttpEndpoint(HttpVerb.Post, "/api/items", OperationName = "Create Item")]
                public record CreateItemRequest(string Name) : IRequest;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(d => d.Id == "NOF203");
    }
}
