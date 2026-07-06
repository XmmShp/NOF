using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NOF.Contract;
using NOF.Hosting;
using NOF.Hosting.SourceGenerator;
using System.Collections.Immutable;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public sealed class RequestOutboundMiddlewareRpcClientInjectionAnalyzerTests
{
    private static readonly Type[] _refs =
    [
        typeof(IRpcClient),
        typeof(IRequestOutboundMiddleware)
    ];

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var extraReferences = _refs.Select(type => type.ToMetadataReference()).ToArray();
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new RequestOutboundMiddlewareRpcClientInjectionAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task RequestOutboundMiddleware_WithDirectRpcClientInjection_ReportsNOF400()
    {
        const string source = """
            using NOF.Contract;
            using NOF.Hosting;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Test;

            public interface IAuthServiceClient : IRpcClient
            {
                Task<Result> RefreshAsync(object request, CancellationToken cancellationToken = default);
            }

            public sealed class RefreshTokenOutboundMiddleware(IAuthServiceClient authServiceClient) : IRequestOutboundMiddleware
            {
                public TopologyComparison Compare(IRequestOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

                public ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken)
                    => next(context, request, cancellationToken);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "NOF400" && diagnostic.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task RequestOutboundMiddleware_WithLazyRpcClient_DoesNotReportNOF400()
    {
        const string source = """
            using NOF.Contract;
            using NOF.Hosting;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Test;

            public interface IAuthServiceClient : IRpcClient
            {
                Task<Result> RefreshAsync(object request, CancellationToken cancellationToken = default);
            }

            public sealed class RefreshTokenOutboundMiddleware(Lazy<IAuthServiceClient> authServiceClient) : IRequestOutboundMiddleware
            {
                public TopologyComparison Compare(IRequestOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

                public ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken)
                    => next(context, request, cancellationToken);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "NOF400");
    }

    [Fact]
    public async Task RequestOutboundMiddleware_WithIServiceProvider_DoesNotReportNOF400()
    {
        const string source = """
            using NOF.Contract;
            using NOF.Hosting;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Test;

            public interface IAuthServiceClient : IRpcClient
            {
                Task<Result> RefreshAsync(object request, CancellationToken cancellationToken = default);
            }

            public sealed class RefreshTokenOutboundMiddleware(IServiceProvider serviceProvider) : IRequestOutboundMiddleware
            {
                public TopologyComparison Compare(IRequestOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

                public ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken)
                    => next(context, request, cancellationToken);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "NOF400");
    }

    [Fact]
    public async Task NonMiddleware_WithDirectRpcClientInjection_DoesNotReportNOF400()
    {
        const string source = """
            using NOF.Contract;

            namespace Test;

            public interface IAuthServiceClient : IRpcClient;

            public sealed class RefreshTokenHelper(IAuthServiceClient authServiceClient);
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "NOF400");
    }
}
