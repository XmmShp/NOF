using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Application;
using NOF.Hosting;
using Xunit;

namespace NOF.Infrastructure.Tests.Steps;

public sealed class RpcServerTransportInitializationStepTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldPassHostAndEveryRegistrationToTransportsInTopologyOrder()
    {
        var calls = new List<TransportCall>();
        var firstRegistration = new RpcServerRegistration(typeof(IFirstService), typeof(FirstServer));
        var secondRegistration = new RpcServerRegistration(typeof(ISecondService), typeof(SecondServer));
        var registry = new RpcServerRegistry();
        registry.Add(firstRegistration);
        registry.Add(secondRegistration);

        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddSingleton<IRpcServerTransport>(new RecordingTransport("second", calls));
        services.AddSingleton<IRpcServerTransport>(new RecordingTransport("first", calls));

        using var provider = services.BuildServiceProvider();
        using var host = new TestHost(provider);
        var step = new RpcServerTransportInitializationStep();

        await step.ExecuteAsync(host);

        Assert.Equal(
            [
                ("first", firstRegistration),
                ("first", secondRegistration),
                ("second", firstRegistration),
                ("second", secondRegistration)
            ],
            calls.Select(static call => (call.TransportName, call.Registration)));
        Assert.All(calls, call => Assert.Same(host, call.Host));
    }

    [Fact]
    public void AddRpcServerTransport_ShouldRegisterTransportOnlyOnce()
    {
        var services = new ServiceCollection();

        services.AddRpcServerTransport<RegisteredTransport>();
        services.AddRpcServerTransport<RegisteredTransport>();

        using var provider = services.BuildServiceProvider();
        Assert.Single(provider.GetServices<IRpcServerTransport>());
    }

    private interface IFirstService;

    private interface ISecondService;

    private sealed class FirstServer;

    private sealed class SecondServer;

    private sealed record TransportCall(
        IHost Host,
        string TransportName,
        RpcServerRegistration Registration);

    private sealed class RecordingTransport(
        string name,
        ICollection<TransportCall> calls) : IRpcServerTransport
    {
        public TopologyComparison Compare(IRpcServerTransport other)
        {
            if (other is not RecordingTransport recording)
            {
                return TopologyComparison.DoesNotMatter;
            }

            return (name, recording.Name) switch
            {
                ("first", "second") => TopologyComparison.Before,
                ("second", "first") => TopologyComparison.After,
                _ => TopologyComparison.DoesNotMatter
            };
        }

        public Task MapAsync(IHost host, RpcServerRegistration registration)
        {
            calls.Add(new TransportCall(host, name, registration));
            return Task.CompletedTask;
        }

        private string Name => name;
    }

    private sealed class RegisteredTransport : IRpcServerTransport
    {
        public RegisteredTransport()
        {
        }

        public TopologyComparison Compare(IRpcServerTransport other)
            => TopologyComparison.DoesNotMatter;

        public Task MapAsync(IHost host, RpcServerRegistration registration)
            => Task.CompletedTask;
    }

    private sealed class TestHost(IServiceProvider services) : IHost
    {
        public IServiceProvider Services { get; } = services;

        public Task StartAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Dispose()
        {
        }
    }
}
