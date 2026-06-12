using NOF.Hosting;
using Xunit;

namespace NOF.Infrastructure.Tests.Utilities;

public interface IStepNode : ITopologizable<IStepNode>;

public class StepA : IStepNode
{
    public TopologyComparison Compare(IStepNode other) => TopologyComparison.DoesNotMatter;
}

public class StepB : IStepNode
{
    public TopologyComparison Compare(IStepNode other)
        => other is StepA ? TopologyComparison.After : TopologyComparison.DoesNotMatter;
}

public class StepC : IStepNode
{
    public TopologyComparison Compare(IStepNode other)
        => other is StepB ? TopologyComparison.After : TopologyComparison.DoesNotMatter;
}

public class StepRunsBeforeC : IStepNode
{
    public TopologyComparison Compare(IStepNode other)
        => other is StepC ? TopologyComparison.Before : TopologyComparison.DoesNotMatter;
}

public class CircularStepA : IStepNode
{
    public TopologyComparison Compare(IStepNode other)
        => other is CircularStepB ? TopologyComparison.After : TopologyComparison.DoesNotMatter;
}

public class CircularStepB : IStepNode
{
    public TopologyComparison Compare(IStepNode other)
        => other is CircularStepA ? TopologyComparison.After : TopologyComparison.DoesNotMatter;
}

public class DependencyGraphTests
{
    [Fact]
    public void Constructor_WithEmptyConfigurators_ShouldCreateEmptyGraph()
    {
        var graph = new DependencyGraph<IStepNode>([]);
        Assert.Empty(graph.GetExecutionOrder());
    }

    [Fact]
    public void GetExecutionOrder_WithSimpleDependency_ShouldOrderCorrectly()
    {
        var taskA = new StepA();
        var taskB = new StepB();
        var graph = new DependencyGraph<IStepNode>([
            taskB,
            taskA
        ]);

        var executionOrder = graph.GetExecutionOrder().ToList();
        Assert.True(executionOrder.IndexOf(taskA) < executionOrder.IndexOf(taskB));
    }

    [Fact]
    public void GetExecutionOrder_WithBeforeRelation_ShouldOrderCorrectly()
    {
        var taskC = new StepC();
        var taskBefore = new StepRunsBeforeC();
        var graph = new DependencyGraph<IStepNode>([
            taskC,
            taskBefore
        ]);

        var executionOrder = graph.GetExecutionOrder().ToList();
        Assert.True(executionOrder.IndexOf(taskBefore) < executionOrder.IndexOf(taskC));
    }

    [Fact]
    public void GetExecutionOrder_WithCircularDependency_ShouldThrowInvalidOperationException()
    {
        var graph = new DependencyGraph<IStepNode>([
            new CircularStepA(),
            new CircularStepB()
        ]);

        var ex = Assert.Throws<InvalidOperationException>(() => graph.GetExecutionOrder());
        Assert.Contains("Circular dependency", ex.Message);
    }
}
