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

public interface IStepE;
public class StepWithMissingDependency : IAfter<IStepE>;

public class DependencyGraphTests
{
    [Fact]
    public void Constructor_WithEmptyConfigurators_ShouldCreateEmptyGraph()
    {
        var graph = new DependencyGraph([]);
        Assert.Empty(graph.GetExecutionOrder());
    }

    [Fact]
    public void GetExecutionOrder_WithSimpleDependency_ShouldOrderCorrectly()
    {
        var taskA = new StepA();
        var taskB = new StepB();
        var graph = new DependencyGraph([
            new(taskB, DependencyNode.CollectRelatedTypes<StepB>()),
            new(taskA, DependencyNode.CollectRelatedTypes<StepA>())
        ]);

        var executionOrder = graph.GetExecutionOrder().Select(n => n.ExtraInfo).ToList();
        Assert.True(executionOrder.IndexOf(taskA) < executionOrder.IndexOf(taskB));
    }

    [Fact]
    public void GetExecutionOrder_WithCircularDependency_ShouldThrowInvalidOperationException()
    {
        var graph = new DependencyGraph([
            new(new CircularStepA(), DependencyNode.CollectRelatedTypes<CircularStepA>()),
            new(new CircularStepB(), DependencyNode.CollectRelatedTypes<CircularStepB>())
        ]);

        var ex = Assert.Throws<InvalidOperationException>(() => graph.GetExecutionOrder());
        Assert.Contains("Circular dependency", ex.Message);
    }

    [Fact]
    public void GetExecutionOrder_WithMissingDependency_ShouldIgnoreMissingDependency()
    {
        var taskA = new StepA();
        var taskWithMissingDep = new StepWithMissingDependency();
        var graph = new DependencyGraph([
            new(taskWithMissingDep, DependencyNode.CollectRelatedTypes<StepWithMissingDependency>()),
            new(taskA, DependencyNode.CollectRelatedTypes<StepA>())
        ]);

        var executionOrder = graph.GetExecutionOrder().Select(n => n.ExtraInfo).ToList();
        Assert.Contains(taskA, executionOrder);
        Assert.Contains(taskWithMissingDep, executionOrder);
    }
}
