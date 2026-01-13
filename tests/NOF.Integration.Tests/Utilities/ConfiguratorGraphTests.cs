using FluentAssertions;
using Xunit;

namespace NOF.Infrastructure.Tests.Utilities;

// Test task interfaces and implementations
public interface IStepA : IStep;
public interface IStepB : IStep;
public interface IStepC : IStep;
public interface IStepD : IStep;

public class StepA : IStepA;
public class StepB : IStepB, IAfter<IStepA>;
public class StepC : IStepC, IAfter<IStepB>;
public class StepD : IStepD, IAfter<IStepA>, IAfter<IStepC>;

// Circular dependency test tasks
public class CircularStepA : IStep, IAfter<CircularStepB>;
public class CircularStepB : IStep, IAfter<CircularStepA>;

// Multiple dependencies test tasks
public class MultiDepsStep : IStep, IAfter<IStepA>, IAfter<IStepB>;

// No dependency task
public class IndependentStep : IStep;

// Multiple tasks implementing same interface
public class StepA1 : IStepA;
public class StepA2 : IStepA;
public class StepDependsOnMultipleA : IStep, IAfter<IStepA>;

// Missing dependency test - dependency not in graph
public interface IStepE : IStep;
public class StepWithMissingDependency : IStep, IAfter<IStepE>;

public class ConfiguratorGraphTests
{
    [Fact]
    public void Constructor_WithEmptyConfigurators_ShouldCreateEmptyGraph()
    {
        // Arrange & Act
        var graph = new ConfiguratorGraph<IStep>([]);

        // Assert
        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithSingleConfigurator_ShouldCreateGraphWithOneConfigurator()
    {
        // Arrange
        var taskA = new StepA();

        // Act
        var graph = new ConfiguratorGraph<IStep>([taskA]);

        // Assert
        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().ContainSingle()
            .Which.Should().Be(taskA);
    }

    [Fact]
    public void Constructor_WithDuplicateConfigurators_ShouldIgnoreDuplicates()
    {
        // Arrange
        var taskA = new StepA();

        // Act
        var graph = new ConfiguratorGraph<IStep>([taskA, taskA, taskA]);

        // Assert
        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().ContainSingle()
            .Which.Should().Be(taskA);
    }

    [Fact]
    public void GetExecutionOrder_WithNoDependencies_ShouldReturnAllConfigurators()
    {
        // Arrange
        var taskA = new StepA();
        var taskB = new IndependentStep();
        var taskC = new IndependentStep();
        var graph = new ConfiguratorGraph<IStep>([taskA, taskB, taskC]);

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
        var taskA = new StepA();
        var taskB = new StepB();
        var graph = new ConfiguratorGraph<IStep>([taskB, taskA]); // Intentionally reversed order

        // Act
        var executionOrder = graph.GetExecutionOrder().ToList();

        // Assert
        executionOrder.Should().HaveCount(2);
        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(taskB),
            "ConfiguratorA should be executed before ConfiguratorB");
    }

    [Fact]
    public void GetExecutionOrder_WithChainedDependencies_ShouldOrderCorrectly()
    {
        // Arrange
        var taskA = new StepA();
        var taskB = new StepB();
        var taskC = new StepC();
        var graph = new ConfiguratorGraph<IStep>([taskC, taskA, taskB]); // Random order

        // Act
        var executionOrder = graph.GetExecutionOrder().ToList();

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
        var taskA = new StepA();
        var taskB = new StepB();
        var taskC = new StepC();
        var taskD = new StepD();
        var graph = new ConfiguratorGraph<IStep>([taskD, taskC, taskB, taskA]); // Reversed order

        // Act
        var executionOrder = graph.GetExecutionOrder().ToList();

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
        var taskA = new CircularStepA();
        var taskB = new CircularStepB();
        var graph = new ConfiguratorGraph<IStep>([taskA, taskB]);

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
        var taskA = new StepA();
        var taskB = new StepB();
        var multiDepsConfigurator = new MultiDepsStep();
        var graph = new ConfiguratorGraph<IStep>([multiDepsConfigurator, taskB, taskA]);

        // Act
        var executionOrder = graph.GetExecutionOrder().ToList();

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
        var taskA = new StepA();
        var taskB = new StepB();
        var independent = new IndependentStep();
        var graph = new ConfiguratorGraph<IStep>([taskB, independent, taskA]);

        // Act
        var executionOrder = graph.GetExecutionOrder().ToList();

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
        var taskA1 = new StepA1();
        var taskA2 = new StepA2();
        var dependentConfigurator = new StepDependsOnMultipleA();
        var graph = new ConfiguratorGraph<IStep>([dependentConfigurator, taskA2, taskA1]);

        // Act
        var executionOrder = graph.GetExecutionOrder().ToList();

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
        var taskA = new StepA();
        var taskB = new StepB();
        var taskC = new StepC();
        var taskD = new StepD();
        var independent = new IndependentStep();
        var graph = new ConfiguratorGraph<IStep>([independent, taskD, taskC, taskB, taskA]);

        // Act
        var executionOrder = graph.GetExecutionOrder().ToList();

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
        var taskA = new StepA();
        var taskB = new StepB();
        var taskC = new StepC();
        var graph = new ConfiguratorGraph<IStep>([taskC, taskB, taskA]);

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
        var task1 = new IndependentStep();
        var task2 = new IndependentStep();
        var task3 = new IndependentStep();
        var graph = new ConfiguratorGraph<IStep>([task1, task2, task3]);

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
        var taskA = new StepA();
        var taskB = new StepB(); // depends on ConfiguratorA
        var taskC = new StepC(); // depends on ConfiguratorB
        var taskD = new StepD(); // depends on ConfiguratorA and ConfiguratorC
        var graph = new ConfiguratorGraph<IStep>([taskD, taskC, taskB, taskA]);

        // Act
        var executionOrder = graph.GetExecutionOrder().ToList();

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
        var taskA = new StepA();
        var taskWithMissingDep = new StepWithMissingDependency();
        var graph = new ConfiguratorGraph<IStep>([taskWithMissingDep, taskA]);

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
