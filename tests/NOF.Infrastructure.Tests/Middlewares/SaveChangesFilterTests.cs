using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace NOF.Infrastructure.Tests.Middlewares;

public class SaveChangesFilterTests
{
    public class TestMessage
    {
        public string Content { get; set; } = string.Empty;
    }

    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class TestDbContext : DbContext
    {
        public DbSet<TestEntity> TestEntities { get; set; } = null!;

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }
    }

    private class TestProbeContext : ProbeContext
    {
        public void Add(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void Add(string key, object value)
        {
            throw new NotImplementedException();
        }

        public void Set(object values)
        {
            throw new NotImplementedException();
        }

        public void Set(IEnumerable<KeyValuePair<string, object>> values)
        {
            throw new NotImplementedException();
        }

        public ProbeContext CreateScope(string key)
        {
            throw new NotImplementedException();
        }

        public CancellationToken CancellationToken { get; }
    }

    [Fact]
    public async Task Send_ShouldCallNextAndSaveChanges()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new TestDbContext(options);
        var filter = new SaveChangesFilter<TestMessage>(dbContext);

        var mockConsumeContext = new Mock<ConsumeContext<TestMessage>>();
        mockConsumeContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        var mockPipe = new Mock<IPipe<ConsumeContext<TestMessage>>>();
        mockPipe.Setup(p => p.Send(It.IsAny<ConsumeContext<TestMessage>>()))
            .Callback(() =>
            {
                // Simulate adding an entity during message processing
                dbContext.TestEntities.Add(new TestEntity { Id = 1, Name = "Test" });
            })
            .Returns(Task.CompletedTask);

        // Act
        await filter.Send(mockConsumeContext.Object, mockPipe.Object);

        // Assert
        mockPipe.Verify(p => p.Send(mockConsumeContext.Object), Times.Once);
        dbContext.TestEntities.Should().HaveCount(1);
        dbContext.TestEntities.First().Name.Should().Be("Test");
    }

    [Fact]
    public async Task Send_ShouldRespectCancellationToken()
    {
        // Arrange
        var mockDbContext = new Mock<DbContext>();
        var filter = new SaveChangesFilter<TestMessage>(mockDbContext.Object);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockConsumeContext = new Mock<ConsumeContext<TestMessage>>();
        mockConsumeContext.Setup(c => c.CancellationToken).Returns(cts.Token);

        var mockPipe = new Mock<IPipe<ConsumeContext<TestMessage>>>();
        mockPipe.Setup(p => p.Send(It.IsAny<ConsumeContext<TestMessage>>()))
            .Returns(Task.CompletedTask);

        mockDbContext
            .Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(0);
            });

        // Act
        var act = () => filter.Send(mockConsumeContext.Object, mockPipe.Object);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Send_WhenNextThrows_ShouldNotSaveChanges()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new TestDbContext(options);
        var filter = new SaveChangesFilter<TestMessage>(dbContext);

        var mockConsumeContext = new Mock<ConsumeContext<TestMessage>>();
        mockConsumeContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        var mockPipe = new Mock<IPipe<ConsumeContext<TestMessage>>>();
        mockPipe.Setup(p => p.Send(It.IsAny<ConsumeContext<TestMessage>>()))
            .Callback(() =>
            {
                dbContext.TestEntities.Add(new TestEntity { Id = 1, Name = "Test" });
                throw new InvalidOperationException("Processing failed");
            });

        // Act
        var act = async () => await filter.Send(mockConsumeContext.Object, mockPipe.Object);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        dbContext.TestEntities.Should().BeEmpty(); // Changes should not be saved
    }

    [Fact]
    public async Task Send_MultipleChanges_ShouldSaveAll()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new TestDbContext(options);
        var filter = new SaveChangesFilter<TestMessage>(dbContext);

        var mockConsumeContext = new Mock<ConsumeContext<TestMessage>>();
        mockConsumeContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        var mockPipe = new Mock<IPipe<ConsumeContext<TestMessage>>>();
        mockPipe.Setup(p => p.Send(It.IsAny<ConsumeContext<TestMessage>>()))
            .Callback(() =>
            {
                dbContext.TestEntities.Add(new TestEntity { Id = 1, Name = "Test1" });
                dbContext.TestEntities.Add(new TestEntity { Id = 2, Name = "Test2" });
                dbContext.TestEntities.Add(new TestEntity { Id = 3, Name = "Test3" });
            })
            .Returns(Task.CompletedTask);

        // Act
        await filter.Send(mockConsumeContext.Object, mockPipe.Object);

        // Assert
        dbContext.TestEntities.Should().HaveCount(3);
    }

    [Fact]
    public async Task Send_NoChanges_ShouldStillCallSaveChanges()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new TestDbContext(options);
        var filter = new SaveChangesFilter<TestMessage>(dbContext);

        var mockConsumeContext = new Mock<ConsumeContext<TestMessage>>();
        mockConsumeContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        var mockPipe = new Mock<IPipe<ConsumeContext<TestMessage>>>();
        mockPipe.Setup(p => p.Send(It.IsAny<ConsumeContext<TestMessage>>()))
            .Returns(Task.CompletedTask);

        // Act
        await filter.Send(mockConsumeContext.Object, mockPipe.Object);

        // Assert
        mockPipe.Verify(p => p.Send(mockConsumeContext.Object), Times.Once);
        // No exception should be thrown even with no changes
    }
}
