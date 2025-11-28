using FluentAssertions;
using MassTransit.Mediator;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace NOF.Infrastructure.Tests.Abstractions;

public class AppDbContextTests
{
    private class TestAggregateRoot : IAggregateRoot
    {
        public long Id { get; init; }
        private readonly List<IEvent> _events = [];
        public IReadOnlyList<IEvent> Events => _events.AsReadOnly();

        public void AddEvent(IEvent @event)
        {
            _events.Add(@event);
        }

        public void ClearEvents()
        {
            _events.Clear();
        }
    }

    private class TestEvent : IEvent
    {
        public string Message { get; set; } = string.Empty;
    }

    private class TestDbContext : AppDbContext
    {
        public DbSet<TestAggregateRoot> TestEntities { get; set; } = null!;

        public TestDbContext(DbContextOptions<TestDbContext> options, IScopedMediator mediator)
            : base(options, mediator)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TestAggregateRoot>().Ignore(e => e.Events);
        }
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldPublishDomainEvents()
    {
        // Arrange
        var mockMediator = new Mock<IScopedMediator>();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new TestDbContext(options, mockMediator.Object);

        var testEvent = new TestEvent { Message = "Test Event" };
        var entity = new TestAggregateRoot { Id = 1 };
        entity.AddEvent(testEvent);

        context.TestEntities.Add(entity);

        // Act
        await context.SaveChangesAsync();

        // Assert
        mockMediator.Verify(m => m.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        entity.Events.Should().BeEmpty(); // Events should be cleared after publishing
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldClearEventsAfterPublishing()
    {
        // Arrange
        var mockMediator = new Mock<IScopedMediator>();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new TestDbContext(options, mockMediator.Object);

        var entity = new TestAggregateRoot { Id = 1 };
        entity.AddEvent(new TestEvent { Message = "Event 1" });
        entity.AddEvent(new TestEvent { Message = "Event 2" });

        context.TestEntities.Add(entity);

        // Act
        await context.SaveChangesAsync();

        // Assert
        entity.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldHandleMultipleAggregatesWithEvents()
    {
        // Arrange
        var mockMediator = new Mock<IScopedMediator>();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new TestDbContext(options, mockMediator.Object);

        var entity1 = new TestAggregateRoot { Id = 1 };
        entity1.AddEvent(new TestEvent { Message = "Event 1" });

        var entity2 = new TestAggregateRoot { Id = 2 };
        entity2.AddEvent(new TestEvent { Message = "Event 2" });

        context.TestEntities.AddRange(entity1, entity2);

        // Act
        await context.SaveChangesAsync();

        // Assert
        mockMediator.Verify(m => m.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        entity1.Events.Should().BeEmpty();
        entity2.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldIgnorePublishExceptions()
    {
        // Arrange
        var mockMediator = new Mock<IScopedMediator>();
        mockMediator.Setup(m => m.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Publish failed"));

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new TestDbContext(options, mockMediator.Object);

        var entity = new TestAggregateRoot { Id = 1 };
        entity.AddEvent(new TestEvent { Message = "Test Event" });

        context.TestEntities.Add(entity);

        // Act
        var act = async () => await context.SaveChangesAsync();

        // Assert - Should not throw exception
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldReturnCorrectRowCount()
    {
        // Arrange
        var mockMediator = new Mock<IScopedMediator>();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new TestDbContext(options, mockMediator.Object);

        var entity1 = new TestAggregateRoot { Id = 1 };
        var entity2 = new TestAggregateRoot { Id = 2 };

        context.TestEntities.AddRange(entity1, entity2);

        // Act
        var result = await context.SaveChangesAsync();

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldNotPublishWhenNoEvents()
    {
        // Arrange
        var mockMediator = new Mock<IScopedMediator>();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new TestDbContext(options, mockMediator.Object);

        var entity = new TestAggregateRoot { Id = 1 };
        context.TestEntities.Add(entity);

        // Act
        await context.SaveChangesAsync();

        // Assert
        mockMediator.Verify(m => m.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
