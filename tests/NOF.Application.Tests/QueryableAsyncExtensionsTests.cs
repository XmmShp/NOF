using Xunit;

namespace NOF.Application.Tests;

public class QueryableAsyncExtensionsTests
{
    private sealed record Order(int Id, decimal Amount);
    private sealed record Product(int Id, string Name);

    [Fact]
    public async Task ToListAsync_ShouldFallbackToSyncQueryable()
    {
        var query = Enumerable.Range(1, 3).AsQueryable();

        var result = await query.ToListAsync();

        Assert.Equal([1, 2, 3], result);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ShouldApplyPredicate()
    {
        var query = Enumerable.Range(1, 5).AsQueryable();

        var result = await query.FirstOrDefaultAsync(value => value > 3);

        Assert.Equal(4, result);
    }

    [Fact]
    public async Task AnyAsync_ShouldReturnFalse_WhenNoMatchExists()
    {
        var query = Enumerable.Range(1, 5).AsQueryable();

        var result = await query.AnyAsync(value => value > 10);

        Assert.False(result);
    }

    [Fact]
    public async Task CountAsync_ShouldWorkOnWrappedQueryableChain()
    {
        var query = Enumerable.Range(1, 10).AsQueryable().AsAsyncQueryable();

        var result = await query.Where(value => value % 2 == 0).CountAsync();

        Assert.Equal(5, result);
    }

    [Fact]
    public async Task AverageAsync_ShouldApplySelector()
    {
        var query = new[]
        {
            new Order(1, 10m),
            new Order(2, 20m),
            new Order(3, 30m)
        }.AsQueryable();

        var result = await query.AverageAsync(order => order.Amount);

        Assert.Equal(20m, result);
    }

    [Fact]
    public async Task ToDictionaryAsync_ShouldMaterializeByKey()
    {
        var query = new[]
        {
            new Order(1, 10m),
            new Order(2, 20m)
        }.AsQueryable();

        var result = await query.ToDictionaryAsync(order => order.Id);

        Assert.Equal(2, result.Count);
        Assert.Equal(20m, result[2].Amount);
    }

    [Fact]
    public async Task AllAsync_ShouldEvaluatePredicate()
    {
        var query = Enumerable.Range(1, 5).AsQueryable();

        var result = await query.AllAsync(value => value > 0);

        Assert.True(result);
    }

    [Fact]
    public async Task ExecuteDeleteAsync_ShouldThrow_WhenProviderDoesNotSupportSetBasedDelete()
    {
        var query = new[]
        {
            new Product(1, "A"),
            new Product(2, "B")
        }.AsQueryable();

        await Assert.ThrowsAsync<NotSupportedException>(() => query.ExecuteDeleteAsync());
    }

    [Fact]
    public async Task ExecuteUpdateAsync_ShouldThrow_WhenProviderDoesNotSupportSetBasedUpdate()
    {
        var query = new[]
        {
            new Product(1, "A"),
            new Product(2, "B")
        }.AsQueryable();

        await Assert.ThrowsAsync<NotSupportedException>(() => query.ExecuteUpdateAsync(
            setters => setters.SetProperty(product => product.Name, "Updated")));
    }
}
