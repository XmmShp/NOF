namespace NOF.Application;

/// <summary>
/// Represents a queryable source whose asynchronous terminal operations are provided by an adapter-specific executor.
/// </summary>
public interface IAsyncQueryable<out TSource> : IQueryable<TSource>
{
    IAsyncQueryExecutor AsyncExecutor { get; }
}
