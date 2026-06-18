using NOF.Application.Data;
using Xunit;

namespace NOF.Application.Tests;

public class QueryableAsyncExtensionsTests
{
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
}
