using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace NOF.Hosting.Abstraction.Tests;

public class InitializationStepServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInitializationStep_WithInstance_ShouldRegisterExactSingleton()
    {
        var services = new ServiceCollection();
        var step = new TestInitializationStep("first");

        services.AddInitializationStep(step);

        using var provider = services.BuildServiceProvider();
        var registered = Assert.Single(provider.GetServices<IApplicationInitializationStep>());
        Assert.Same(step, registered);
    }

    [Fact]
    public void TryAddInitializationStep_WithType_ShouldAvoidDuplicates()
    {
        var services = new ServiceCollection();

        services.TryAddInitializationStep<ParameterlessInitializationStep>();
        services.TryAddInitializationStep<ParameterlessInitializationStep>();

        using var provider = services.BuildServiceProvider();
        Assert.Single(provider.GetServices<IApplicationInitializationStep>().OfType<ParameterlessInitializationStep>());
    }

    [Fact]
    public void RemoveInitializationStep_WithPredicate_ShouldRemoveMatchingInstanceRegistration()
    {
        var services = new ServiceCollection();

        services.AddInitializationStep(new TestInitializationStep("keep"));
        services.AddInitializationStep(new TestInitializationStep("remove"));

        services.RemoveInitializationStep<TestInitializationStep>(step => step.Name == "remove");

        using var provider = services.BuildServiceProvider();
        var steps = provider.GetServices<IApplicationInitializationStep>().OfType<TestInitializationStep>().ToList();
        var remaining = Assert.Single(steps);
        Assert.Equal("keep", remaining.Name);
    }

    private sealed class TestInitializationStep(string name) : IApplicationInitializationStep
    {
        public string Name { get; } = name;

        public TopologyComparison Compare(IApplicationInitializationStep other)
            => TopologyComparison.DoesNotMatter;

        public Task ExecuteAsync(IHost app) => Task.CompletedTask;
    }

    private sealed class ParameterlessInitializationStep : IApplicationInitializationStep
    {
        public TopologyComparison Compare(IApplicationInitializationStep other)
            => TopologyComparison.DoesNotMatter;

        public Task ExecuteAsync(IHost app) => Task.CompletedTask;
    }
}
