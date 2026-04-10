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
        typeof(IRpcService),
        typeof(Result),
        typeof(Result<>),
        typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection),
        typeof(NOFInfrastructureExtensions),
        typeof(IInboundPipelineExecutor),
        typeof(Hosting.IOutboundPipelineExecutor)
    ];

    [Fact]
    public void GeneratedCode_InterceptsAddSplitInterfaceService_AndRegistersGeneratedImplementation()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using NOF.Application;
            using NOF.Contract;
            using NOF.Infrastructure;
            using System.Threading;
            using System.Threading.Tasks;

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

                public static class Program
                {
                    public static void Configure(IServiceCollection services)
                    {
                        services.AddSplitInterfaceService<IMyService, MyService>();
                    }
                }
            }
            """;

        var result = new SplitInterfaceServiceGenerator().GetResult(source, _refs);
        var generatedCode = string.Join("\n\n", result.GeneratedTrees.Select(tree => tree.GetRoot().ToFullString()));

        Assert.Contains("InterceptsLocation", generatedCode);
        Assert.Contains("AddSplitInterfaceService_App_IMyService_App_MyService", generatedCode);
        Assert.Contains("ServiceCollectionDescriptorExtensions.Replace(", generatedCode);
        Assert.Contains("ServiceDescriptor.Scoped<global::App.IMyService", generatedCode);
        Assert.Contains(": global::App.IMyService", generatedCode);
    }

    [Fact]
    public void GeneratedCode_UsesOutboundThenInboundPattern()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using NOF.Application;
            using NOF.Contract;
            using NOF.Infrastructure;
            using System.Threading;
            using System.Threading.Tasks;

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

                public static class Program
                {
                    public static void Configure(IServiceCollection services)
                    {
                        services.AddSplitInterfaceService<IMyService, MyService>();
                    }
                }
            }
            """;

        var result = new SplitInterfaceServiceGenerator().GetResult(source, _refs);
        var generatedCode = string.Join("\n\n", result.GeneratedTrees.Select(tree => tree.GetRoot().ToFullString()));

        Assert.Contains("_outboundPipeline.ExecuteAsync", generatedCode);
        Assert.Contains("InboundHandlerInvoker.ExecuteRpcAsync(", generatedCode);
        Assert.Contains("context.Metadatas[\"HandlerType\"] = handlerType;", generatedCode);
        Assert.Contains("PingAsync(", generatedCode);
        Assert.Contains("EchoAsync(", generatedCode);
    }
}
