using NOF.Abstraction;
using Xunit;

namespace NOF.Hosting.Abstraction.Tests;

public interface IConcernNode : ITopologizable<IConcernNode>;

public class ConcernAProvider : IConcernNode
{
    public TopologyComparison Compare(IConcernNode other) => TopologyComparison.DoesNotMatter;
}

public class ConcernBAfterA : IConcernNode
{
    public TopologyComparison Compare(IConcernNode other)
        => other is ConcernAProvider ? TopologyComparison.After : TopologyComparison.DoesNotMatter;
}

public class ConcernCAfterB : IConcernNode
{
    public TopologyComparison Compare(IConcernNode other)
        => other is ConcernBAfterA ? TopologyComparison.After : TopologyComparison.DoesNotMatter;
}

public class DependencyGraphTests
{
    [Fact]
    public void GetExecutionOrder_WithUnrelatedNodes_ShouldIncludeAllNodes()
    {
        var first = new ConcernAProvider();
        var second = new ConcernAProvider();
        var third = new ConcernAProvider();

        var graph = new DependencyGraph<IConcernNode>([
            first,
            second,
            third
        ]);

        var executionOrder = graph.GetExecutionOrder().ToList();

        Assert.Contains(first, executionOrder);
        Assert.Contains(second, executionOrder);
        Assert.Contains(third, executionOrder);
    }

    [Fact]
    public void GetExecutionOrder_WithInstanceComparisons_ShouldHonorPartialOrder()
    {
        var provider = new ConcernAProvider();
        var middle = new ConcernBAfterA();
        var follower = new ConcernCAfterB();

        var graph = new DependencyGraph<IConcernNode>([
            follower,
            middle,
            provider
        ]);

        var executionOrder = graph.GetExecutionOrder().ToList();

        Assert.True(executionOrder.IndexOf(provider) < executionOrder.IndexOf(middle));
        Assert.True(executionOrder.IndexOf(middle) < executionOrder.IndexOf(follower));
    }
}
