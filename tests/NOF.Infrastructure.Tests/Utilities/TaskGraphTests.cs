using FluentAssertions;
using Xunit;

namespace NOF.Infrastructure.Tests.Utilities;

// Test task interfaces and implementations
public interface ITaskA : ITask;
public interface ITaskB : ITask;
public interface ITaskC : ITask;
public interface ITaskD : ITask;

public class TaskA : ITaskA;
public class TaskB : ITaskB, IDepsOn<ITaskA>;
public class TaskC : ITaskC, IDepsOn<ITaskB>;
public class TaskD : ITaskD, IDepsOn<ITaskA>, IDepsOn<ITaskC>;

// Circular dependency test tasks
public class CircularTaskA : ITask, IDepsOn<CircularTaskB>;
public class CircularTaskB : ITask, IDepsOn<CircularTaskA>;

// Multiple dependencies test tasks
public class MultiDepsTask : ITask, IDepsOn<ITaskA>, IDepsOn<ITaskB>;

// No dependency task
public class IndependentTask : ITask;

// Multiple tasks implementing same interface
public class TaskA1 : ITaskA;
public class TaskA2 : ITaskA;
public class TaskDependsOnMultipleA : ITask, IDepsOn<ITaskA>;

public class TaskGraphTests
{
    [Fact]
    public void Constructor_WithEmptyTasks_ShouldCreateEmptyGraph()
    {
        // Arrange & Act
        var graph = new TaskGraph([]);

        // Assert
        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithSingleTask_ShouldCreateGraphWithOneTask()
    {
        // Arrange
        var taskA = new TaskA();

        // Act
        var graph = new TaskGraph([taskA]);

        // Assert
        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().ContainSingle()
            .Which.Should().Be(taskA);
    }

    [Fact]
    public void Constructor_WithDuplicateTasks_ShouldIgnoreDuplicates()
    {
        // Arrange
        var taskA = new TaskA();

        // Act
        var graph = new TaskGraph([taskA, taskA, taskA]);

        // Assert
        var executionOrder = graph.GetExecutionOrder();
        executionOrder.Should().ContainSingle()
            .Which.Should().Be(taskA);
    }

    [Fact]
    public void GetExecutionOrder_WithNoDependencies_ShouldReturnAllTasks()
    {
        // Arrange
        var taskA = new TaskA();
        var taskB = new IndependentTask();
        var taskC = new IndependentTask();
        var graph = new TaskGraph([taskA, taskB, taskC]);

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
        var taskA = new TaskA();
        var taskB = new TaskB();
        var graph = new TaskGraph([taskB, taskA]); // Intentionally reversed order

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(2);
        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(taskB),
            "TaskA should be executed before TaskB");
    }

    [Fact]
    public void GetExecutionOrder_WithChainedDependencies_ShouldOrderCorrectly()
    {
        // Arrange
        var taskA = new TaskA();
        var taskB = new TaskB();
        var taskC = new TaskC();
        var graph = new TaskGraph([taskC, taskA, taskB]); // Random order

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(3);
        var indexA = executionOrder.IndexOf(taskA);
        var indexB = executionOrder.IndexOf(taskB);
        var indexC = executionOrder.IndexOf(taskC);

        indexA.Should().BeLessThan(indexB, "TaskA should be executed before TaskB");
        indexB.Should().BeLessThan(indexC, "TaskB should be executed before TaskC");
    }

    [Fact]
    public void GetExecutionOrder_WithMultipleDependencies_ShouldOrderCorrectly()
    {
        // Arrange
        var taskA = new TaskA();
        var taskB = new TaskB();
        var taskC = new TaskC();
        var taskD = new TaskD();
        var graph = new TaskGraph([taskD, taskC, taskB, taskA]); // Reversed order

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(4);
        var indexA = executionOrder.IndexOf(taskA);
        var indexB = executionOrder.IndexOf(taskB);
        var indexC = executionOrder.IndexOf(taskC);
        var indexD = executionOrder.IndexOf(taskD);

        // TaskD depends on TaskA and TaskC
        indexA.Should().BeLessThan(indexD, "TaskA should be executed before TaskD");
        indexC.Should().BeLessThan(indexD, "TaskC should be executed before TaskD");

        // TaskC depends on TaskB
        indexB.Should().BeLessThan(indexC, "TaskB should be executed before TaskC");

        // TaskB depends on TaskA
        indexA.Should().BeLessThan(indexB, "TaskA should be executed before TaskB");
    }

    [Fact]
    public void GetExecutionOrder_WithCircularDependency_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var taskA = new CircularTaskA();
        var taskB = new CircularTaskB();
        var graph = new TaskGraph([taskA, taskB]);

        // Act
        var act = () => graph.GetExecutionOrder();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Circular dependency*");
    }

    [Fact]
    public void GetExecutionOrder_WithMultipleDependenciesOnSameTask_ShouldOrderCorrectly()
    {
        // Arrange
        var taskA = new TaskA();
        var taskB = new TaskB();
        var multiDepsTask = new MultiDepsTask();
        var graph = new TaskGraph([multiDepsTask, taskB, taskA]);

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(3);
        var indexA = executionOrder.IndexOf(taskA);
        var indexB = executionOrder.IndexOf(taskB);
        var indexMulti = executionOrder.IndexOf(multiDepsTask);

        indexA.Should().BeLessThan(indexMulti, "TaskA should be executed before MultiDepsTask");
        indexB.Should().BeLessThan(indexMulti, "TaskB should be executed before MultiDepsTask");
    }

    [Fact]
    public void GetExecutionOrder_WithMixedDependentAndIndependentTasks_ShouldOrderCorrectly()
    {
        // Arrange
        var taskA = new TaskA();
        var taskB = new TaskB();
        var independent = new IndependentTask();
        var graph = new TaskGraph([taskB, independent, taskA]);

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(3);
        executionOrder.IndexOf(taskA).Should().BeLessThan(executionOrder.IndexOf(taskB),
            "TaskA should be executed before TaskB");
        // Independent task can be anywhere in the order
        executionOrder.Should().Contain(independent);
    }

    [Fact]
    public void GetExecutionOrder_WithMultipleTasksImplementingSameInterface_ShouldResolveDependenciesCorrectly()
    {
        // Arrange
        var taskA1 = new TaskA1();
        var taskA2 = new TaskA2();
        var dependentTask = new TaskDependsOnMultipleA();
        var graph = new TaskGraph([dependentTask, taskA2, taskA1]);

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(3);
        var indexA1 = executionOrder.IndexOf(taskA1);
        var indexA2 = executionOrder.IndexOf(taskA2);
        var indexDependent = executionOrder.IndexOf(dependentTask);

        indexA1.Should().BeLessThan(indexDependent, "TaskA1 should be executed before dependent task");
        indexA2.Should().BeLessThan(indexDependent, "TaskA2 should be executed before dependent task");
    }

    [Fact]
    public void GetExecutionOrder_WithComplexDependencyGraph_ShouldOrderCorrectly()
    {
        // Arrange
        // Create a complex graph:
        // TaskA (no deps)
        // TaskB depends on TaskA
        // TaskC depends on TaskB
        // TaskD depends on TaskA and TaskC
        // Independent task (no deps)
        var taskA = new TaskA();
        var taskB = new TaskB();
        var taskC = new TaskC();
        var taskD = new TaskD();
        var independent = new IndependentTask();
        var graph = new TaskGraph([independent, taskD, taskC, taskB, taskA]);

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
        var taskA = new TaskA();
        var taskB = new TaskB();
        var taskC = new TaskC();
        var graph = new TaskGraph([taskC, taskB, taskA]);

        // Act
        var executionOrder1 = graph.GetExecutionOrder();
        var executionOrder2 = graph.GetExecutionOrder();
        var executionOrder3 = graph.GetExecutionOrder();

        // Assert
        executionOrder1.Should().Equal(executionOrder2);
        executionOrder2.Should().Equal(executionOrder3);
    }

    [Fact]
    public void GetExecutionOrder_WithOnlyIndependentTasks_ShouldReturnAllTasks()
    {
        // Arrange
        var task1 = new IndependentTask();
        var task2 = new IndependentTask();
        var task3 = new IndependentTask();
        var graph = new TaskGraph([task1, task2, task3]);

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
        //     TaskA
        //    /     \
        // TaskB   TaskC (independent)
        //    \     /
        //     TaskD
        var taskA = new TaskA();
        var taskB = new TaskB(); // depends on TaskA
        var taskC = new TaskC(); // depends on TaskB
        var taskD = new TaskD(); // depends on TaskA and TaskC
        var graph = new TaskGraph([taskD, taskC, taskB, taskA]);

        // Act
        var executionOrder = graph.GetExecutionOrder();

        // Assert
        executionOrder.Should().HaveCount(4);
        var indexA = executionOrder.IndexOf(taskA);
        var indexB = executionOrder.IndexOf(taskB);
        var indexC = executionOrder.IndexOf(taskC);
        var indexD = executionOrder.IndexOf(taskD);

        // TaskA must come before TaskB and TaskD
        indexA.Should().BeLessThan(indexB);
        indexA.Should().BeLessThan(indexD);

        // TaskB must come before TaskC
        indexB.Should().BeLessThan(indexC);

        // TaskC must come before TaskD
        indexC.Should().BeLessThan(indexD);
    }
}
