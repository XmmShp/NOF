using NOF.Hosting;
using Xunit;

namespace NOF.Infrastructure.Tests.Utilities;

public interface IStepA;
public interface IStepB;
public interface IStepC;
public interface IStepD;

public class StepA : IStepA;
public class StepB : IStepB, IAfter<IStepA>;
public class StepC : IStepC, IAfter<IStepB>;
public class StepD : IStepD, IAfter<IStepA>, IAfter<IStepC>;

public class CircularStepA : IAfter<CircularStepB>;
public class CircularStepB : IAfter<CircularStepA>;

public class MultiDepsStep : IAfter<IStepA>, IAfter<IStepB>;

public class IndependentStep;

public class StepA1 : IStepA;
public class StepA2 : IStepA;
public class StepDependsOnMultipleA : IAfter<IStepA>;

public interface IStepE;
public class StepWithMissingDependency : IAfter<IStepE>;

public class DependencyGraphTests
{
    [Fact]
    public void Constructor_WithEmptyConfigurators_ShouldCreateEmptyGraph()
    {
        var graph = new DependencyGraph<object>([]);

        var executionOrder = graph.GetExecutionOrder();
        Assert.Empty(executionOrder);
    }

    [Fact]
    public void Constructor_WithSingleConfigurator_ShouldCreateGraphWithOneConfigurator()
    {
        var taskA = new StepA();
        var graph = new DependencyGraph<object>([new(taskA, DependencyNode<object>.CollectRelatedTypes<StepA>())]);

        var executionOrder = graph.GetExecutionOrder();
        Assert.Equal(taskA, Assert.Single(executionOrder));
    }

    [Fact]
    public void Constructor_WithDuplicateConfigurators_ShouldIgnoreDuplicates()
    {
        var taskA = new StepA();
        var node = new DependencyNode<object>(taskA, DependencyNode<object>.CollectRelatedTypes<StepA>());
        var graph = new DependencyGraph<object>([node, node, node]);

        var executionOrder = graph.GetExecutionOrder();
        Assert.Equal(taskA, Assert.Single(executionOrder));
    }

    [Fact]
    public void GetExecutionOrder_WithNoDependencies_ShouldReturnAllConfigurators()
    {
        var taskA = new StepA();
        var taskB = new IndependentStep();
        var taskC = new IndependentStep();
        var graph = new DependencyGraph<object>([
            new(taskA, DependencyNode<object>.CollectRelatedTypes<StepA>()),
            new(taskB, DependencyNode<object>.CollectRelatedTypes<IndependentStep>()),
            new(taskC, DependencyNode<object>.CollectRelatedTypes<IndependentStep>())
        ]);

        var executionOrder = graph.GetExecutionOrder();

        Assert.Equal(3, executionOrder.Count());
        Assert.Contains(taskA, executionOrder);
        Assert.Contains(taskB, executionOrder);
        Assert.Contains(taskC, executionOrder);
    }

    [Fact]
    public void GetExecutionOrder_WithSimpleDependency_ShouldOrderCorrectly()
    {
        var taskA = new StepA();
        var taskB = new StepB();
        var graph = new DependencyGraph<object>([
            new(taskB, DependencyNode<object>.CollectRelatedTypes<StepB>()),
            new(taskA, DependencyNode<object>.CollectRelatedTypes<StepA>())
        ]);

        var executionOrder = graph.GetExecutionOrder().ToList();

        Assert.Equal(2, executionOrder.Count());
        Assert.True(executionOrder.IndexOf(taskA) < executionOrder.IndexOf(taskB));
    }

    [Fact]
    public void GetExecutionOrder_WithChainedDependencies_ShouldOrderCorrectly()
    {
        var taskA = new StepA();
        var taskB = new StepB();
        var taskC = new StepC();
        var graph = new DependencyGraph<object>([
            new(taskC, DependencyNode<object>.CollectRelatedTypes<StepC>()),
            new(taskA, DependencyNode<object>.CollectRelatedTypes<StepA>()),
            new(taskB, DependencyNode<object>.CollectRelatedTypes<StepB>())
        ]);

        var executionOrder = graph.GetExecutionOrder().ToList();

        Assert.True(executionOrder.IndexOf(taskA) < executionOrder.IndexOf(taskB));
        Assert.True(executionOrder.IndexOf(taskB) < executionOrder.IndexOf(taskC));
    }

    [Fact]
    public void GetExecutionOrder_WithMultipleDependencies_ShouldOrderCorrectly()
    {
        var taskA = new StepA();
        var taskB = new StepB();
        var taskC = new StepC();
        var taskD = new StepD();
        var graph = new DependencyGraph<object>([
            new(taskD, DependencyNode<object>.CollectRelatedTypes<StepD>()),
            new(taskC, DependencyNode<object>.CollectRelatedTypes<StepC>()),
            new(taskB, DependencyNode<object>.CollectRelatedTypes<StepB>()),
            new(taskA, DependencyNode<object>.CollectRelatedTypes<StepA>())
        ]);

        var executionOrder = graph.GetExecutionOrder().ToList();

        Assert.True(executionOrder.IndexOf(taskA) < executionOrder.IndexOf(taskB));
        Assert.True(executionOrder.IndexOf(taskB) < executionOrder.IndexOf(taskC));
        Assert.True(executionOrder.IndexOf(taskA) < executionOrder.IndexOf(taskD));
        Assert.True(executionOrder.IndexOf(taskC) < executionOrder.IndexOf(taskD));
    }

    [Fact]
    public void GetExecutionOrder_WithCircularDependency_ShouldThrowInvalidOperationException()
    {
        var graph = new DependencyGraph<object>([
            new(new CircularStepA(), DependencyNode<object>.CollectRelatedTypes<CircularStepA>()),
            new(new CircularStepB(), DependencyNode<object>.CollectRelatedTypes<CircularStepB>())
        ]);

        var act = () => graph.GetExecutionOrder();

        var ex = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("Circular dependency", ex.Message);
    }

    [Fact]
    public void GetExecutionOrder_WithMissingDependency_ShouldIgnoreMissingDependency()
    {
        var taskA = new StepA();
        var taskWithMissingDep = new StepWithMissingDependency();
        var graph = new DependencyGraph<object>([
            new(taskWithMissingDep, DependencyNode<object>.CollectRelatedTypes<StepWithMissingDependency>()),
            new(taskA, DependencyNode<object>.CollectRelatedTypes<StepA>())
        ]);

        var executionOrder = graph.GetExecutionOrder();

        Assert.Equal(2, executionOrder.Count());
        Assert.Contains(taskA, executionOrder);
        Assert.Contains(taskWithMissingDep, executionOrder);
    }
}
