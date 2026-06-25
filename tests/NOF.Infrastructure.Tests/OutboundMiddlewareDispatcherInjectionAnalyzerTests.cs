using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NOF.Application;
using NOF.Infrastructure;
using NOF.Infrastructure.SourceGenerator;
using System.Collections.Immutable;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public sealed class OutboundMiddlewareDispatcherInjectionAnalyzerTests
{
    private static readonly Type[] _refs =
    [
        typeof(ICommandSender),
        typeof(INotificationPublisher),
        typeof(ICommandOutboundMiddleware),
        typeof(INotificationOutboundMiddleware)
    ];

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var extraReferences = _refs.Select(type => type.ToMetadataReference()).ToArray();
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new OutboundMiddlewareDispatcherInjectionAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task CommandOutboundMiddleware_WithDirectCommandSenderInjection_ReportsNOF305()
    {
        const string source = """
            using NOF.Application;
            using NOF.Infrastructure;
            using NOF.Hosting;
            using System.Threading;

            namespace Test;

            public sealed class RefreshTokenCommandOutboundMiddleware(ICommandSender commandSender) : ICommandOutboundMiddleware
            {
                public TopologyComparison Compare(ICommandOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

                public ValueTask InvokeAsync(CommandOutboundContext context, object message, CommandOutboundHandlerDelegate next, CancellationToken cancellationToken)
                    => next(context, message, cancellationToken);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "NOF305" && diagnostic.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task NotificationOutboundMiddleware_WithDirectNotificationPublisherInjection_ReportsNOF305()
    {
        const string source = """
            using NOF.Application;
            using NOF.Infrastructure;
            using NOF.Hosting;
            using System.Threading;

            namespace Test;

            public sealed class RefreshTokenNotificationOutboundMiddleware(INotificationPublisher notificationPublisher) : INotificationOutboundMiddleware
            {
                public TopologyComparison Compare(INotificationOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

                public ValueTask InvokeAsync(NotificationOutboundContext context, object message, NotificationOutboundHandlerDelegate next, CancellationToken cancellationToken)
                    => next(context, message, cancellationToken);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "NOF305" && diagnostic.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task CommandOutboundMiddleware_WithLazyCommandSender_DoesNotReportNOF305()
    {
        const string source = """
            using NOF.Application;
            using NOF.Infrastructure;
            using NOF.Hosting;
            using System;
            using System.Threading;

            namespace Test;

            public sealed class RefreshTokenCommandOutboundMiddleware(Lazy<ICommandSender> commandSender) : ICommandOutboundMiddleware
            {
                public TopologyComparison Compare(ICommandOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

                public ValueTask InvokeAsync(CommandOutboundContext context, object message, CommandOutboundHandlerDelegate next, CancellationToken cancellationToken)
                    => next(context, message, cancellationToken);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "NOF305");
    }

    [Fact]
    public async Task NotificationOutboundMiddleware_WithIServiceProvider_DoesNotReportNOF305()
    {
        const string source = """
            using NOF.Application;
            using System;
            using NOF.Infrastructure;
            using NOF.Hosting;
            using System.Threading;

            namespace Test;

            public sealed class RefreshTokenNotificationOutboundMiddleware(IServiceProvider serviceProvider) : INotificationOutboundMiddleware
            {
                public TopologyComparison Compare(INotificationOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

                public ValueTask InvokeAsync(NotificationOutboundContext context, object message, NotificationOutboundHandlerDelegate next, CancellationToken cancellationToken)
                    => next(context, message, cancellationToken);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "NOF305");
    }
}
