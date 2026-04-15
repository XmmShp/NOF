using NOF.Abstraction;
using NOF.Annotation;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using NOF.Infrastructure.SourceGenerator;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class SplitInterfaceServiceGeneratorTests
{
    private static readonly Type[] _refs =
    [
        typeof(ISplitedInterface<>),
        typeof(AssemblyInitializeAttribute),
        typeof(Registry),
        typeof(AutoInjectServiceRegistration),
        typeof(IRpcService),
        typeof(Result),
        typeof(Result<>),
        typeof(SplitInterfaceServiceAttribute<,>),
        typeof(IExecutionContext),
        typeof(Hosting.IOutboundPipelineExecutor),
        typeof(Hosting.OutboundContext),
        typeof(InboundHandlerInvoker),
        typeof(Microsoft.Extensions.DependencyInjection.IServiceProviderIsService)
    ];

    [Fact]
    public void GeneratedCode_RegistersGeneratedImplementationViaAssemblyInitializer()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;
            using NOF.Infrastructure;
            using System.Threading;
            using System.Threading.Tasks;

            [assembly: NOF.Infrastructure.SplitInterfaceService<App.IMyService, App.MyService>]

            namespace App
            {
                public record PingRequest(string Value);

                public partial interface IMyService : IRpcService
                {
                    Task<Result> PingAsync(PingRequest request, CancellationToken cancellationToken = default);
                }

                public partial class MyService : ISplitedInterface<IMyService>;

                public partial class MyService
                {
                    public interface Ping
                    {
                        Task<Result> PingAsync(PingRequest request, CancellationToken cancellationToken);
                    }
                }

                public sealed class PingHandler : MyService.Ping
                {
                    public Task<Result> PingAsync(PingRequest request, CancellationToken cancellationToken)
                        => Task.FromResult(Result.Success());
                }
            }
            """;

        var result = new SplitInterfaceServiceGenerator().GetResultPostGen(source, _refs);
        var generatedCode = string.Join("\n\n", result.GeneratedTrees.Select(tree => tree.GetRoot().ToFullString()));

        Assert.Contains("AssemblyInitializeAttribute<", generatedCode);
        Assert.Contains("SplitInterfaceServiceAssemblyInitializer", generatedCode);
        Assert.Contains("Registry.AutoInjectRegistrations.Add(new global::NOF.Annotation.AutoInjectServiceRegistration(typeof(global::App.IMyService), typeof(", generatedCode);
        Assert.Contains("Lifetime.Scoped", generatedCode);
        Assert.Contains(": global::App.IMyService", generatedCode);
    }

    [Fact]
    public void GeneratedCode_UsesOutboundThenInboundPattern()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;
            using NOF.Infrastructure;
            using System.Threading;
            using System.Threading.Tasks;

            [assembly: NOF.Infrastructure.SplitInterfaceService<App.IMyService, App.MyService>]

            namespace App
            {
                public record PingRequest(string Value);
                public record EchoRequest(string Value);

                public partial interface IMyService : IRpcService
                {
                    Task<Result> PingAsync(PingRequest request, CancellationToken cancellationToken = default);
                    Task<Result<string>> EchoAsync(EchoRequest request, CancellationToken cancellationToken = default);
                }

                public partial class MyService : ISplitedInterface<IMyService>;

                public partial class MyService
                {
                    public interface Ping
                    {
                        Task<Result> PingAsync(PingRequest request, CancellationToken cancellationToken);
                    }

                    public interface Echo
                    {
                        Task<Result<string>> EchoAsync(EchoRequest request, CancellationToken cancellationToken);
                    }
                }

                public sealed class PingHandler : MyService.Ping
                {
                    public Task<Result> PingAsync(PingRequest request, CancellationToken cancellationToken)
                        => Task.FromResult(Result.Success());
                }

                public sealed class EchoHandler : MyService.Echo
                {
                    public Task<Result<string>> EchoAsync(EchoRequest request, CancellationToken cancellationToken)
                        => Task.FromResult(Result.Success(request.Value));
                }
            }
            """;

        var result = new SplitInterfaceServiceGenerator().GetResultPostGen(source, _refs);
        var generatedCode = string.Join("\n\n", result.GeneratedTrees.Select(tree => tree.GetRoot().ToFullString()));

        Assert.Contains("_outboundPipeline.ExecuteAsync", generatedCode);
        Assert.Contains("InboundHandlerInvoker.ExecuteRpcAsync(", generatedCode);
        Assert.Contains("context.Metadatas[\"HandlerType\"] = handlerType;", generatedCode);
        Assert.Contains("IServiceProviderIsService", generatedCode);
        Assert.Contains("PingAsync(", generatedCode);
        Assert.Contains("EchoAsync(", generatedCode);
    }
}
