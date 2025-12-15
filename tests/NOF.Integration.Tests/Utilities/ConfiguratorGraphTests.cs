using FluentAssertions;
using Xunit;

namespace NOF.Infrastructure.Tests.Utilities;

// Test task interfaces and implementations
public interface IConfigA : IConfig;
public interface IConfigB : IConfig;
public interface IConfigC : IConfig;
public interface IConfigD : IConfig;

public class ConfigA : IConfigA;
public class ConfigB : IConfigB, IDepsOn<IConfigA>;
public class ConfigC : IConfigC, IDepsOn<IConfigB>;
public class ConfigD : IConfigD, IDepsOn<IConfigA>, IDepsOn<IConfigC>;

// Circular dependency test tasks
public class CircularConfigA : IConfig, IDepsOn<CircularConfigB>;
public class CircularConfigB : IConfig, IDepsOn<CircularConfigA>;

// Multiple dependencies test tasks
public class MultiDepsConfig : IConfig, IDepsOn<IConfigA>, IDepsOn<IConfigB>;

// No dependency task
public class IndependentConfig : IConfig;

// Multiple tasks implementing same interface
public class ConfigA1 : IConfigA;
public class ConfigA2 : IConfigA;
public class ConfigDependsOnMultipleA : IConfig, IDepsOn<IConfigA>;

// Missing dependency test - dependency not in graph
public interface IConfigE : IConfig;
public class ConfigWithMissingDependency : IConfig, IDepsOn<IConfigE>;

public class ConfiguratorGraphTests
{
    [Fact]
    public void Constructor_WithEmptyConfigurators_ShouldCreateEmptyGraph()
    {
        // Arrange & Act
        var graph = new ConfiguratorGraph<IConfig>([]);

        // Assert
        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithSingleConfigurator_ShouldCreateGraphWithOneConfigurator()
    {
        // Arrange
        var taskA = new ConfigA();

        // Act
        var graph = new ConfiguratorGraph<IConfig>([taskA]);

        // Assert
        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().ContainSingle()
            .Which.Should().Be(taskA);
    }

    [Fact]
    public void Constructor_WithDuplicateConfigurators_ShouldIgnoreDuplicates()
    {
        // Arrange
        var taskA = new ConfigA();

        // Act
        var graph = new ConfiguratorGraph<IConfig>([taskA, taskA, taskA]);

        // Assert
        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().ContainSingle()
            .Which.Should().Be(taskA);
    }

    [Fact]
    public void GetExecutionOrder_WithNoDependencies_ShouldReturnAllConfigurators()
    {
        // Arrange
        var taskA = new ConfigA();
        var taskB = new IndependentConfig();
        var taskC = new IndependentConfig();
        var graph = new ConfiguratorGraph<IConfig>([taskA, taskB, taskC]);

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
        var taskA = new ConfigA();
        var taskB = new ConfigB();
        var graph = new ConfiguratorGraph<IConfig>([taskB, taskA]); // Intentionally reversed order

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
        var taskA = new ConfigA();
        var taskB = new ConfigB();
        var taskC = new ConfigC();
        var graph = new ConfiguratorGraph<IConfig>([taskC, taskA, taskB]); // Random order

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
        var taskA = new ConfigA();
        var taskB = new ConfigB();
        var taskC = new ConfigC();
        var taskD = new ConfigD();
        var graph = new ConfiguratorGraph<IConfig>([taskD, taskC, taskB, taskA]); // Reversed order

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
        var taskA = new CircularConfigA();
        var taskB = new CircularConfigB();
        var graph = new ConfiguratorGraph<IConfig>([taskA, taskB]);

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
        var taskA = new ConfigA();
        var taskB = new ConfigB();
        var multiDepsConfigurator = new MultiDepsConfig();
        var graph = new ConfiguratorGraph<IConfig>([multiDepsConfigurator, taskB, taskA]);

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
        var taskA = new ConfigA();
        var taskB = new ConfigB();
        var independent = new IndependentConfig();
        var graph = new ConfiguratorGraph<IConfig>([taskB, independent, taskA]);

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
        var taskA1 = new ConfigA1();
        var taskA2 = new ConfigA2();
        var dependentConfigurator = new ConfigDependsOnMultipleA();
        var graph = new ConfiguratorGraph<IConfig>([dependentConfigurator, taskA2, taskA1]);

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
        var taskA = new ConfigA();
        var taskB = new ConfigB();
        var taskC = new ConfigC();
        var taskD = new ConfigD();
        var independent = new IndependentConfig();
        var graph = new ConfiguratorGraph<IConfig>([independent, taskD, taskC, taskB, taskA]);

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
        var taskA = new ConfigA();
        var taskB = new ConfigB();
        var taskC = new ConfigC();
        var graph = new ConfiguratorGraph<IConfig>([taskC, taskB, taskA]);

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
        var task1 = new IndependentConfig();
        var task2 = new IndependentConfig();
        var task3 = new IndependentConfig();
        var graph = new ConfiguratorGraph<IConfig>([task1, task2, task3]);

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
        var taskA = new ConfigA();
        var taskB = new ConfigB(); // depends on ConfiguratorA
        var taskC = new ConfigC(); // depends on ConfiguratorB
        var taskD = new ConfigD(); // depends on ConfiguratorA and ConfiguratorC
        var graph = new ConfiguratorGraph<IConfig>([taskD, taskC, taskB, taskA]);

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
        var taskA = new ConfigA();
        var taskWithMissingDep = new ConfigWithMissingDependency();
        var graph = new ConfiguratorGraph<IConfig>([taskWithMissingDep, taskA]);

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
