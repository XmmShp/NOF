using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace NOF.Infrastructure.Tests.Abstractions;

public class RepositoryTests
{
    private class TestAggregateRoot : IAggregateRoot
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public IReadOnlyList<IEvent> Events => [];
        public void ClearEvents() { }
    }

    private class TestRepository : Repository<TestAggregateRoot>
    {
        public TestRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }

    private class TestDbContext : DbContext
    {
        public DbSet<TestAggregateRoot> TestEntities { get; set; } = null!;

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TestAggregateRoot>().Ignore(e => e.Events);
        }
    }

    [Fact]
    public void Add_ShouldAddEntityToDbContext()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new TestDbContext(options);
        var repository = new TestRepository(context);
        var entity = new TestAggregateRoot { Id = 1, Name = "Test" };

        // Act
        repository.Add(entity);

        // Assert
        context.TestEntities.Local.Should().Contain(entity);
        context.Entry(entity).State.Should().Be(EntityState.Added);
    }

    [Fact]
    public void Remove_ShouldMarkEntityAsDeleted()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new TestDbContext(options);
        var repository = new TestRepository(context);
        var entity = new TestAggregateRoot { Id = 1, Name = "Test" };

        context.TestEntities.Add(entity);
        context.SaveChanges();
        context.Entry(entity).State = EntityState.Unchanged;

        // Act
        repository.Remove(entity);

        // Assert
        context.Entry(entity).State.Should().Be(EntityState.Deleted);
    }

    [Fact]
    public async Task FindAsync_ShouldReturnEntityWhenExists()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new TestDbContext(options);
        var repository = new TestRepository(context);
        var entity = new TestAggregateRoot { Id = 1, Name = "Test" };

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.FindAsync(1, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Name.Should().Be("Test");
    }

    [Fact]
    public async Task FindAsync_ShouldReturnNullWhenNotExists()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new TestDbContext(options);
        var repository = new TestRepository(context);

        // Act
        var result = await repository.FindAsync(999, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindAsync_ShouldRespectCancellationToken()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new TestDbContext(options);
        var repository = new TestRepository(context);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await repository.FindAsync(1, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Add_ShouldHandleMultipleEntities()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new TestDbContext(options);
        var repository = new TestRepository(context);
        var entity1 = new TestAggregateRoot { Id = 1, Name = "Test1" };
        var entity2 = new TestAggregateRoot { Id = 2, Name = "Test2" };

        // Act
        repository.Add(entity1);
        repository.Add(entity2);

        // Assert
        context.TestEntities.Local.Should().HaveCount(2);
        context.TestEntities.Local.Should().Contain(entity1);
        context.TestEntities.Local.Should().Contain(entity2);
    }

    [Fact]
    public void Remove_ShouldHandleMultipleEntities()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new TestDbContext(options);
        var repository = new TestRepository(context);
        var entity1 = new TestAggregateRoot { Id = 1, Name = "Test1" };
        var entity2 = new TestAggregateRoot { Id = 2, Name = "Test2" };

        context.TestEntities.AddRange(entity1, entity2);
        context.SaveChanges();

        // Act
        repository.Remove(entity1);
        repository.Remove(entity2);

        // Assert
        context.Entry(entity1).State.Should().Be(EntityState.Deleted);
        context.Entry(entity2).State.Should().Be(EntityState.Deleted);
    }
}
