using FluentAssertions;
using NOF.Infrastructure.Abstraction;
using NOF.Infrastructure.Core;
using Xunit;

namespace NOF.Infrastructure.Core.Tests.Utilities;

public interface IStepA : IStep;
public interface IStepB : IStep;
public interface IStepC : IStep;
public interface IStepD : IStep;

public class StepA : IStepA, IStep<StepA>;
public class StepB : IStepB, IStep<StepB>, IAfter<IStepA>;
public class StepC : IStepC, IStep<StepC>, IAfter<IStepB>;
public class StepD : IStepD, IStep<StepD>, IAfter<IStepA>, IAfter<IStepC>;

public class CircularStepA : IStep<CircularStepA>, IAfter<CircularStepB>;
public class CircularStepB : IStep<CircularStepB>, IAfter<CircularStepA>;

public class MultiDepsStep : IStep<MultiDepsStep>, IAfter<IStepA>, IAfter<IStepB>;

public class IndependentStep : IStep<IndependentStep>;

public class StepA1 : IStepA, IStep<StepA1>;
public class StepA2 : IStepA, IStep<StepA2>;
public class StepDependsOnMultipleA : IStep<StepDependsOnMultipleA>, IAfter<IStepA>;

public interface IStepE : IStep;
public class StepWithMissingDependency : IStep<StepWithMissingDependency>, IAfter<IStepE>;

public class ConfiguratorGraphTests
{
    [Fact]
    public void Constructor_WithEmptyConfigurators_ShouldCreateEmptyGraph()
    {
        var graph = new ConfiguratorGraph<IStep>([]);

        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithSingleConfigurator_ShouldCreateGraphWithOneConfigurator()
    {
        var taskA = new StepA();

        var graph = new ConfiguratorGraph<IStep>([taskA]);

        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().ContainSingle()
            .Which.Should().Be(taskA);
    }

    [Fact]
    public void Constructor_WithDuplicateConfigurators_ShouldIgnoreDuplicates()
    {
        var taskA = new StepA();

        var graph = new ConfiguratorGraph<IStep>([taskA, taskA, taskA]);

        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().ContainSingle()
            .Which.Should().Be(taskA);
    }

    [Fact]
    public void GetExecutionOrder_WithNoDependencies_ShouldReturnAllConfigurators()
    {
        var taskA = new StepA();
        var taskB = new IndependentStep();
        var taskC = new IndependentStep();
        var graph = new ConfiguratorGraph<IStep>([taskA, taskB, taskC]);

        var executionOrder = graph.GetExecutionOrder();

        executionOrder.Should().HaveCount(3);
        executionOrder.Should().Contain(taskA);
        executionOrder.Should().Contain(taskB);
        executionOrder.Should().Contain(taskC);
    }

    [Fact]
    public void GetExecutionOrder_WithSimpleDependency_ShouldOrderCorrectly()
    {
        var taskA = new StepA();
        var taskB = new StepB();
        var graph = new ConfiguratorGraph<IStep>([taskB, taskA]);

        var executionOrder = graph.GetExecutionOrder().ToList();

        executionOrder.Should().HaveCount(2);
        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(taskB));
    }

    [Fact]
    public void GetExecutionOrder_WithChainedDependencies_ShouldOrderCorrectly()
    {
        var taskA = new StepA();
        var taskB = new StepB();
        var taskC = new StepC();
        var graph = new ConfiguratorGraph<IStep>([taskC, taskA, taskB]);

        var executionOrder = graph.GetExecutionOrder().ToList();

        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(taskB));
        executionOrder.IndexOf(taskB).Should().BeLessThan(executionOrder.IndexOf(taskC));
    }

    [Fact]
    public void GetExecutionOrder_WithMultipleDependencies_ShouldOrderCorrectly()
    {
        var taskA = new StepA();
        var taskB = new StepB();
        var taskC = new StepC();
        var taskD = new StepD();
        var graph = new ConfiguratorGraph<IStep>([taskD, taskC, taskB, taskA]);

        var executionOrder = graph.GetExecutionOrder().ToList();

        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(taskB));
        executionOrder.IndexOf(taskB).Should().BeLessThan(executionOrder.IndexOf(taskC));
        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(taskD));
        executionOrder.IndexOf(taskC).Should().BeLessThan(executionOrder.IndexOf(taskD));
    }

    [Fact]
    public void GetExecutionOrder_WithCircularDependency_ShouldThrowInvalidOperationException()
    {
        var graph = new ConfiguratorGraph<IStep>([new CircularStepA(), new CircularStepB()]);

        var act = () => graph.GetExecutionOrder();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Circular dependency*");
    }

    [Fact]
    public void GetExecutionOrder_WithMultipleDependenciesOnSameConfigurator_ShouldOrderCorrectly()
    {
        var taskA = new StepA();
        var taskB = new StepB();
        var multiDepsConfigurator = new MultiDepsStep();
        var graph = new ConfiguratorGraph<IStep>([multiDepsConfigurator, taskB, taskA]);

        var executionOrder = graph.GetExecutionOrder().ToList();

        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(multiDepsConfigurator));
        executionOrder.IndexOf(taskB).Should().BeLessThan(executionOrder.IndexOf(multiDepsConfigurator));
    }

    [Fact]
    public void GetExecutionOrder_WithMixedDependentAndIndependentConfigurators_ShouldOrderCorrectly()
    {
        var taskA = new StepA();
        var taskB = new StepB();
        var independent = new IndependentStep();
        var graph = new ConfiguratorGraph<IStep>([taskB, independent, taskA]);

        var executionOrder = graph.GetExecutionOrder().ToList();

        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(taskB));
        executionOrder.Should().Contain(independent);
    }

    [Fact]
    public void GetExecutionOrder_WithMultipleConfiguratorsImplementingSameInterface_ShouldResolveDependenciesCorrectly()
    {
        var taskA1 = new StepA1();
        var taskA2 = new StepA2();
        var dependentConfigurator = new StepDependsOnMultipleA();
        var graph = new ConfiguratorGraph<IStep>([dependentConfigurator, taskA2, taskA1]);

        var executionOrder = graph.GetExecutionOrder().ToList();

        executionOrder.IndexOf(taskA1).Should().BeLessThan(executionOrder.IndexOf(dependentConfigurator));
        executionOrder.IndexOf(taskA2).Should().BeLessThan(executionOrder.IndexOf(dependentConfigurator));
    }

    [Fact]
    public void GetExecutionOrder_WithComplexDependencyGraph_ShouldOrderCorrectly()
    {
        var taskA = new StepA();
        var taskB = new StepB();
        var taskC = new StepC();
        var taskD = new StepD();
        var independent = new IndependentStep();
        var graph = new ConfiguratorGraph<IStep>([independent, taskD, taskC, taskB, taskA]);

        var executionOrder = graph.GetExecutionOrder().ToList();

        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(taskB));
        executionOrder.IndexOf(taskB).Should().BeLessThan(executionOrder.IndexOf(taskC));
        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(taskD));
        executionOrder.IndexOf(taskC).Should().BeLessThan(executionOrder.IndexOf(taskD));
    }

    [Fact]
    public void GetExecutionOrder_CalledMultipleTimes_ShouldReturnConsistentResults()
    {
        var taskA = new StepA();
        var taskB = new StepB();
        var taskC = new StepC();
        var graph = new ConfiguratorGraph<IStep>([taskC, taskB, taskA]);

        var executionOrder1 = graph.GetExecutionOrder();
        var executionOrder2 = graph.GetExecutionOrder();
        var executionOrder3 = graph.GetExecutionOrder();

        executionOrder1.Should().Equal(executionOrder2);
        executionOrder2.Should().Equal(executionOrder3);
    }

    [Fact]
    public void GetExecutionOrder_WithOnlyIndependentConfigurators_ShouldReturnAllConfigurators()
    {
        var task1 = new IndependentStep();
        var task2 = new IndependentStep();
        var task3 = new IndependentStep();
        var graph = new ConfiguratorGraph<IStep>([task1, task2, task3]);

        var executionOrder = graph.GetExecutionOrder();

        executionOrder.Should().HaveCount(3);
        executionOrder.Should().Contain([task1, task2, task3]);
    }

    [Fact]
    public void GetExecutionOrder_WithDiamondDependency_ShouldOrderCorrectly()
    {
        var taskA = new StepA();
        var taskB = new StepB();
        var taskC = new StepC();
        var taskD = new StepD();
        var graph = new ConfiguratorGraph<IStep>([taskD, taskC, taskB, taskA]);

        var executionOrder = graph.GetExecutionOrder().ToList();

        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(taskB));
        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(taskD));
        executionOrder.IndexOf(taskB).Should().BeLessThan(executionOrder.IndexOf(taskC));
        executionOrder.IndexOf(taskC).Should().BeLessThan(executionOrder.IndexOf(taskD));
    }

    [Fact]
    public void GetExecutionOrder_WithMissingDependency_ShouldIgnoreMissingDependency()
    {
        var taskA = new StepA();
        var taskWithMissingDep = new StepWithMissingDependency();
        var graph = new ConfiguratorGraph<IStep>([taskWithMissingDep, taskA]);

        var executionOrder = graph.GetExecutionOrder();

        executionOrder.Should().HaveCount(2);
        executionOrder.Should().Contain(taskA);
        executionOrder.Should().Contain(taskWithMissingDep);
    }
}
