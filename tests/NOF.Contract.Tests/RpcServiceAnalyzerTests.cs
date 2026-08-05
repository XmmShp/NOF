using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NOF.Contract;
using NOF.Contract.SourceGenerator;
using System.Collections.Immutable;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class RpcServiceAnalyzerTests
{
    private static readonly Type[] _refs =
    [
        typeof(HttpEndpointAttribute),
        typeof(Empty),
        typeof(IRpcService),
        typeof(HttpVerb),
        typeof(Result),
        typeof(Result<>),
        typeof(IParsable<>),
        typeof(TransportOverAttribute),
        typeof(TransportOverHttpAttribute)
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
    public void TransportOverHttp_ShouldExposeSelectedRpcStyle()
    {
        var attribute = new TransportOverHttpAttribute(HttpRpcStyle.JsonRpc, "/rpc");
        var attributeWithoutPrefix = new TransportOverHttpAttribute(HttpRpcStyle.ControllerRpc);

        Assert.True(typeof(TransportOverAttribute).IsAbstract);
        Assert.Equal(HttpRpcStyle.JsonRpc, attribute.Style);
        Assert.Equal("/rpc", attribute.RoutePrefix);
        Assert.Null(attributeWithoutPrefix.RoutePrefix);
    }

    [Fact]
    public async Task TransportOverHttp_OnRpcServiceContract_ShouldBeValid()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            [TransportOverHttp(HttpRpcStyle.ControllerRpc, "/api")]
            public interface IMyService : IRpcService
            {
                Result Ping(Empty request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id is "NOF211" or "NOF212" or "NOF214");
    }

    [Fact]
    public async Task TransportOverHttp_OnNonRpcServiceInterface_ShouldReportNOF211()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            [TransportOverHttp(HttpRpcStyle.JsonRpc)]
            public interface INotAnRpcService;
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF211");
    }

    [Fact]
    public async Task RpcServiceContract_WithMultipleTransportAttributes_ShouldReportNOF212()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public sealed class TransportOverQueueAttribute : TransportOverAttribute;

            [TransportOverHttp(HttpRpcStyle.JsonRpc)]
            [TransportOverQueue]
            public interface IMyService : IRpcService
            {
                Result Ping(Empty request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF212");
    }

    [Fact]
    public async Task RpcServiceContract_WithoutTransportAttribute_ShouldReportNOF214()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public interface IMyService : IRpcService
            {
                Result Ping(Empty request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF214");
        Assert.Contains(nameof(TransportOverAttribute), diagnostic.GetMessage());
    }

    [Fact]
    public async Task JsonRpcServiceMethod_WithHttpEndpoint_ShouldReportNOF213()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record PingRequest(string Value);

            [TransportOverHttp(HttpRpcStyle.JsonRpc, "/rpc")]
            public interface IMyService : IRpcService
            {
                [HttpEndpoint(HttpVerb.Post, "/ping")]
                Result Ping(PingRequest request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF213");
        Assert.Contains(nameof(HttpEndpointAttribute), diagnostic.GetMessage());
    }

    [Fact]
    public async Task ControllerRpcServiceMethod_WithHttpEndpoint_ShouldNotReportNOF213()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record PingRequest(string Value);

            [TransportOverHttp(HttpRpcStyle.ControllerRpc, "/api")]
            public interface IMyService : IRpcService
            {
                [HttpEndpoint(HttpVerb.Post, "/ping")]
                Result Ping(PingRequest request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF213");
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
    public async Task RouteParametersNotSupported_ReportsNOF201()
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
    public async Task GetEndpoint_WithUnparsableQueryProperty_ReportsNOF203()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public sealed class QueryRequest
            {
                public ComplexValue Value { get; set; } = new();
            }

            public sealed class ComplexValue;

            public partial interface IMyService : IRpcService
            {
                [HttpEndpoint(HttpVerb.Get, "/api/items")]
                Result Get(QueryRequest request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Single(diagnostics, d => d.Id == "NOF203");
    }

    [Fact]
    public async Task GetEndpoint_WithParsableProperty_NoNOF203()
    {
        const string source = """
            using NOF.Contract;
            using System;

            namespace App;

            public sealed class CommandRequest
            {
                public StrongValue Value { get; set; }
            }

            public readonly record struct StrongValue(string Value) : IParsable<StrongValue>
            {
                public static StrongValue Parse(string value, IFormatProvider? provider)
                    => new(value);

                public static bool TryParse(string? value, IFormatProvider? provider, out StrongValue result)
                {
                    result = new StrongValue(value ?? string.Empty);
                    return true;
                }
            }

            public partial interface IMyService : IRpcService
            {
                [HttpEndpoint(HttpVerb.Get, "/api/items")]
                Result Execute(CommandRequest request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "NOF203");
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
    public async Task ServiceMethod_WithCustomReturnAndSingleRequest_ReportsNOF210()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record Query(string Value);
            public record MyResponse(string Value);

            public partial interface IMyService : IRpcService
            {
                MyResponse Get(Query request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Contains(diagnostics, d => d.Id == "NOF210");
    }

    [Fact]
    public async Task ServiceMethod_WithResultOfEmptyReturn_NoSignatureDiagnostics()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record Query(string Value);

            public partial interface IMyService : IRpcService
            {
                Result<Empty> Execute(Query request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, d => d.Id is "NOF207" or "NOF209");
    }

    [Fact]
    public async Task ServiceMethod_WithCustomResultImplementation_NoNOF210()
    {
        const string source = """
            using NOF.Contract;
            using System.Collections.Generic;

            namespace App;

            public record Query(string Value);

            public sealed record CustomResult(bool IsSuccess, string ErrorCode, string Message, object? Value, IDictionary<string, string> Extra) : IResult;

            public partial interface IMyService : IRpcService
            {
                CustomResult Execute(Query request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "NOF210");
    }

    [Fact]
    public async Task ServiceMethod_WithVoidReturn_ReportsNOF209()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record Query(string Value);

            public partial interface IMyService : IRpcService
            {
                void Execute(Query request);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Contains(diagnostics, d => d.Id == "NOF209");
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
