using FluentAssertions;
using Xunit;

namespace NOF.Infrastructure.Tests.Utilities;

// Test task interfaces and implementations
public interface IConfiguratorA : IConfigurator;
public interface IConfiguratorB : IConfigurator;
public interface IConfiguratorC : IConfigurator;
public interface IConfiguratorD : IConfigurator;

public class ConfiguratorA : IConfiguratorA;
public class ConfiguratorB : IConfiguratorB, IDepsOn<IConfiguratorA>;
public class ConfiguratorC : IConfiguratorC, IDepsOn<IConfiguratorB>;
public class ConfiguratorD : IConfiguratorD, IDepsOn<IConfiguratorA>, IDepsOn<IConfiguratorC>;

// Circular dependency test tasks
public class CircularConfiguratorA : IConfigurator, IDepsOn<CircularConfiguratorB>;
public class CircularConfiguratorB : IConfigurator, IDepsOn<CircularConfiguratorA>;

// Multiple dependencies test tasks
public class MultiDepsConfigurator : IConfigurator, IDepsOn<IConfiguratorA>, IDepsOn<IConfiguratorB>;

// No dependency task
public class IndependentConfigurator : IConfigurator;

// Multiple tasks implementing same interface
public class ConfiguratorA1 : IConfiguratorA;
public class ConfiguratorA2 : IConfiguratorA;
public class ConfiguratorDependsOnMultipleA : IConfigurator, IDepsOn<IConfiguratorA>;

// Missing dependency test - dependency not in graph
public interface IConfiguratorE : IConfigurator;
public class ConfiguratorWithMissingDependency : IConfigurator, IDepsOn<IConfiguratorE>;

public class ConfiguratorGraphTests
{
    [Fact]
    public void Constructor_WithEmptyConfigurators_ShouldCreateEmptyGraph()
    {
        // Arrange & Act
        var graph = new ConfiguratorGraph<IConfigurator>([]);

        // Assert
        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithSingleConfigurator_ShouldCreateGraphWithOneConfigurator()
    {
        // Arrange
        var taskA = new ConfiguratorA();

        // Act
        var graph = new ConfiguratorGraph<IConfigurator>([taskA]);

        // Assert
        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().ContainSingle()
            .Which.Should().Be(taskA);
    }

    [Fact]
    public void Constructor_WithDuplicateConfigurators_ShouldIgnoreDuplicates()
    {
        // Arrange
        var taskA = new ConfiguratorA();

        // Act
        var graph = new ConfiguratorGraph<IConfigurator>([taskA, taskA, taskA]);

        // Assert
        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().ContainSingle()
            .Which.Should().Be(taskA);
    }

    [Fact]
    public void GetExecutionOrder_WithNoDependencies_ShouldReturnAllConfigurators()
    {
        // Arrange
        var taskA = new ConfiguratorA();
        var taskB = new IndependentConfigurator();
        var taskC = new IndependentConfigurator();
        var graph = new ConfiguratorGraph<IConfigurator>([taskA, taskB, taskC]);

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(3);
        executionOrder.Should().Contain(taskA);
        executionOrder.Should().Contain(taskB);
        executionOrder.Should().Contain(taskC);
    }

    [Fact]
    public void GetExecutionOrder_WithSimpleDependency_ShouldOrderCorrectly()
    {
        // Arrange
        var taskA = new ConfiguratorA();
        var taskB = new ConfiguratorB();
        var graph = new ConfiguratorGraph<IConfigurator>([taskB, taskA]); // Intentionally reversed order

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(2);
        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(taskB),
            "ConfiguratorA should be executed before ConfiguratorB");
    }

    [Fact]
    public void GetExecutionOrder_WithChainedDependencies_ShouldOrderCorrectly()
    {
        // Arrange
        var taskA = new ConfiguratorA();
        var taskB = new ConfiguratorB();
        var taskC = new ConfiguratorC();
        var graph = new ConfiguratorGraph<IConfigurator>([taskC, taskA, taskB]); // Random order

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(3);
        var indexA = executionOrder.IndexOf(taskA);
        var indexB = executionOrder.IndexOf(taskB);
        var indexC = executionOrder.IndexOf(taskC);

        indexA.Should().BeLessThan(indexB, "ConfiguratorA should be executed before ConfiguratorB");
        indexB.Should().BeLessThan(indexC, "ConfiguratorB should be executed before ConfiguratorC");
    }

    [Fact]
    public void GetExecutionOrder_WithMultipleDependencies_ShouldOrderCorrectly()
    {
        // Arrange
        var taskA = new ConfiguratorA();
        var taskB = new ConfiguratorB();
        var taskC = new ConfiguratorC();
        var taskD = new ConfiguratorD();
        var graph = new ConfiguratorGraph<IConfigurator>([taskD, taskC, taskB, taskA]); // Reversed order

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(4);
        var indexA = executionOrder.IndexOf(taskA);
        var indexB = executionOrder.IndexOf(taskB);
        var indexC = executionOrder.IndexOf(taskC);
        var indexD = executionOrder.IndexOf(taskD);

        // ConfiguratorD depends on ConfiguratorA and ConfiguratorC
        indexA.Should().BeLessThan(indexD, "ConfiguratorA should be executed before ConfiguratorD");
        indexC.Should().BeLessThan(indexD, "ConfiguratorC should be executed before ConfiguratorD");

        // ConfiguratorC depends on ConfiguratorB
        indexB.Should().BeLessThan(indexC, "ConfiguratorB should be executed before ConfiguratorC");

        // ConfiguratorB depends on ConfiguratorA
        indexA.Should().BeLessThan(indexB, "ConfiguratorA should be executed before ConfiguratorB");
    }

    [Fact]
    public void GetExecutionOrder_WithCircularDependency_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var taskA = new CircularConfiguratorA();
        var taskB = new CircularConfiguratorB();
        var graph = new ConfiguratorGraph<IConfigurator>([taskA, taskB]);

        // Act
        var act = () => graph.GetExecutionOrder();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Circular dependency*");
    }

    [Fact]
    public void GetExecutionOrder_WithMultipleDependenciesOnSameConfigurator_ShouldOrderCorrectly()
    {
        // Arrange
        var taskA = new ConfiguratorA();
        var taskB = new ConfiguratorB();
        var multiDepsConfigurator = new MultiDepsConfigurator();
        var graph = new ConfiguratorGraph<IConfigurator>([multiDepsConfigurator, taskB, taskA]);

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(3);
        var indexA = executionOrder.IndexOf(taskA);
        var indexB = executionOrder.IndexOf(taskB);
        var indexMulti = executionOrder.IndexOf(multiDepsConfigurator);

        indexA.Should().BeLessThan(indexMulti, "ConfiguratorA should be executed before MultiDepsConfigurator");
        indexB.Should().BeLessThan(indexMulti, "ConfiguratorB should be executed before MultiDepsConfigurator");
    }

    [Fact]
    public void GetExecutionOrder_WithMixedDependentAndIndependentConfigurators_ShouldOrderCorrectly()
    {
        // Arrange
        var taskA = new ConfiguratorA();
        var taskB = new ConfiguratorB();
        var independent = new IndependentConfigurator();
        var graph = new ConfiguratorGraph<IConfigurator>([taskB, independent, taskA]);

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(3);
        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(taskB),
            "ConfiguratorA should be executed before ConfiguratorB");
        // Independent task can be anywhere in the order
        executionOrder.Should().Contain(independent);
    }

    [Fact]
    public void GetExecutionOrder_WithMultipleConfiguratorsImplementingSameInterface_ShouldResolveDependenciesCorrectly()
    {
        // Arrange
        var taskA1 = new ConfiguratorA1();
        var taskA2 = new ConfiguratorA2();
        var dependentConfigurator = new ConfiguratorDependsOnMultipleA();
        var graph = new ConfiguratorGraph<IConfigurator>([dependentConfigurator, taskA2, taskA1]);

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(3);
        var indexA1 = executionOrder.IndexOf(taskA1);
        var indexA2 = executionOrder.IndexOf(taskA2);
        var indexDependent = executionOrder.IndexOf(dependentConfigurator);

        indexA1.Should().BeLessThan(indexDependent, "ConfiguratorA1 should be executed before dependent task");
        indexA2.Should().BeLessThan(indexDependent, "ConfiguratorA2 should be executed before dependent task");
    }

    [Fact]
    public void GetExecutionOrder_WithComplexDependencyGraph_ShouldOrderCorrectly()
    {
        // Arrange
        // Create a complex graph:
        // ConfiguratorA (no deps)
        // ConfiguratorB depends on ConfiguratorA
        // ConfiguratorC depends on ConfiguratorB
        // ConfiguratorD depends on ConfiguratorA and ConfiguratorC
        // Independent task (no deps)
        var taskA = new ConfiguratorA();
        var taskB = new ConfiguratorB();
        var taskC = new ConfiguratorC();
        var taskD = new ConfiguratorD();
        var independent = new IndependentConfigurator();
        var graph = new ConfiguratorGraph<IConfigurator>([independent, taskD, taskC, taskB, taskA]);

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(5);
        var indexA = executionOrder.IndexOf(taskA);
        var indexB = executionOrder.IndexOf(taskB);
        var indexC = executionOrder.IndexOf(taskC);
        var indexD = executionOrder.IndexOf(taskD);

        // Verify all dependency constraints
        indexA.Should().BeLessThan(indexB);
        indexB.Should().BeLessThan(indexC);
        indexA.Should().BeLessThan(indexD);
        indexC.Should().BeLessThan(indexD);
    }

    [Fact]
    public void GetExecutionOrder_CalledMultipleTimes_ShouldReturnConsistentResults()
    {
        // Arrange
        var taskA = new ConfiguratorA();
        var taskB = new ConfiguratorB();
        var taskC = new ConfiguratorC();
        var graph = new ConfiguratorGraph<IConfigurator>([taskC, taskB, taskA]);

        // Act
        var executionOrder1 = graph.GetExecutionOrder();
        var executionOrder2 = graph.GetExecutionOrder();
        var executionOrder3 = graph.GetExecutionOrder();

        // Assert
        executionOrder1.Should().Equal(executionOrder2);
        executionOrder2.Should().Equal(executionOrder3);
    }

    [Fact]
    public void GetExecutionOrder_WithOnlyIndependentConfigurators_ShouldReturnAllConfigurators()
    {
        // Arrange
        var task1 = new IndependentConfigurator();
        var task2 = new IndependentConfigurator();
        var task3 = new IndependentConfigurator();
        var graph = new ConfiguratorGraph<IConfigurator>([task1, task2, task3]);

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(3);
        executionOrder.Should().Contain([task1, task2, task3]);
    }

    [Fact]
    public void GetExecutionOrder_WithDiamondDependency_ShouldOrderCorrectly()
    {
        // Arrange
        // Diamond pattern:
        //     ConfiguratorA
        //    /     \
        // ConfiguratorB   ConfiguratorC (independent)
        //    \     /
        //     ConfiguratorD
        var taskA = new ConfiguratorA();
        var taskB = new ConfiguratorB(); // depends on ConfiguratorA
        var taskC = new ConfiguratorC(); // depends on ConfiguratorB
        var taskD = new ConfiguratorD(); // depends on ConfiguratorA and ConfiguratorC
        var graph = new ConfiguratorGraph<IConfigurator>([taskD, taskC, taskB, taskA]);

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(4);
        var indexA = executionOrder.IndexOf(taskA);
        var indexB = executionOrder.IndexOf(taskB);
        var indexC = executionOrder.IndexOf(taskC);
        var indexD = executionOrder.IndexOf(taskD);

        // ConfiguratorA must come before ConfiguratorB and ConfiguratorD
        indexA.Should().BeLessThan(indexB);
        indexA.Should().BeLessThan(indexD);

        // ConfiguratorB must come before ConfiguratorC
        indexB.Should().BeLessThan(indexC);

        // ConfiguratorC must come before ConfiguratorD
        indexC.Should().BeLessThan(indexD);
    }

    [Fact]
    public void GetExecutionOrder_WithMissingDependency_ShouldIgnoreMissingDependency()
    {
        // Arrange
        // ConfiguratorWithMissingDependency depends on IConfiguratorE, but no IConfiguratorE is in the graph
        var taskA = new ConfiguratorA();
        var taskWithMissingDep = new ConfiguratorWithMissingDependency();
        var graph = new ConfiguratorGraph<IConfigurator>([taskWithMissingDep, taskA]);

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        // Both tasks should be in the execution order
        executionOrder.Should().HaveCount(2);
        executionOrder.Should().Contain(taskA);
        executionOrder.Should().Contain(taskWithMissingDep);

        // Since the dependency is missing, taskWithMissingDependency should be treated as having no dependencies
        // and can appear anywhere in the order (no constraint on its position)
    }
}
