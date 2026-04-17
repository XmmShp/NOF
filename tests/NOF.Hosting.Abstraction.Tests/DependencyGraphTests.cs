using NOF.Abstraction;
using Xunit;

namespace NOF.Hosting.Abstraction.Tests;

public interface IConcernA;
public interface IConcernB;

public class ConcernAProvider : IConcernA;
public class ConcernBIrrelevant : IConcernB, IAfter<IConcernA>;
public class ConcernAFollowerWithOtherConcernDependency : IConcernA, IAfter<IConcernB>;

public class DependencyGraphTests
{
    [Fact]
    public void GetExecutionOrder_WithDifferentConcernTypes_ShouldIgnoreNonFocusedContracts()
    {
        var provider = new ConcernAProvider();
        var irrelevant = new ConcernBIrrelevant();
        var follower = new ConcernAFollowerWithOtherConcernDependency();

        var graph = new DependencyGraph<IConcernA>([
            new(provider, typeof(ConcernAProvider).GetAllAssignableTypes()),
            new(irrelevant, typeof(ConcernBIrrelevant).GetAllAssignableTypes()),
            new(follower, typeof(ConcernAFollowerWithOtherConcernDependency).GetAllAssignableTypes())
        ]);

        var executionOrder = graph.GetExecutionOrder().Select(n => n.ExtraInfo).ToList();

        Assert.Contains(provider, executionOrder);
        Assert.Contains(irrelevant, executionOrder);
        Assert.Contains(follower, executionOrder);
    }
}
