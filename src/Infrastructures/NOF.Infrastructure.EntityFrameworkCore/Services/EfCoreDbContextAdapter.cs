using Microsoft.EntityFrameworkCore;
using NOF.Application;
using System.Linq.Expressions;
using System.Reflection;

namespace NOF.Infrastructure;

internal sealed class EfCoreDbContextAdapter(DbContext dbContext) : IDbContext
{
    private readonly DbContext _dbContext = dbContext;
    private readonly EfCoreAsyncQueryExecutor _asyncExecutor = new();

    public IDbSet<TEntity> Set<TEntity>()
        where TEntity : class
        => new EfCoreDbSetAdapter<TEntity>(_dbContext.Set<TEntity>(), _asyncExecutor);

    public int SaveChanges()
        => _dbContext.SaveChanges();

    public int SaveChanges(bool acceptAllChangesOnSuccess)
        => _dbContext.SaveChanges(acceptAllChangesOnSuccess);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
}

internal sealed class EfCoreDbSetAdapter<TEntity> : AsyncQueryable<TEntity>, IDbSet<TEntity>
    where TEntity : class
{
    private readonly DbSet<TEntity> _dbSet;

    public EfCoreDbSetAdapter(DbSet<TEntity> dbSet, IAsyncQueryExecutor asyncExecutor) : base(dbSet, asyncExecutor)
    {
        _dbSet = dbSet;
    }

    public void Add(TEntity entity)
        => _dbSet.Add(entity);

    public ValueTask AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => new(_dbSet.AddAsync(entity, cancellationToken).AsTask());

    public void AddRange(IEnumerable<TEntity> entities)
        => _dbSet.AddRange(entities);

    public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        => _dbSet.AddRangeAsync(entities, cancellationToken);

    public void Attach(TEntity entity)
        => _dbSet.Attach(entity);

    public void AttachRange(IEnumerable<TEntity> entities)
        => _dbSet.AttachRange(entities);

    public void Update(TEntity entity)
        => _dbSet.Update(entity);

    public void UpdateRange(IEnumerable<TEntity> entities)
        => _dbSet.UpdateRange(entities);

    public void Remove(TEntity entity)
        => _dbSet.Remove(entity);

    public void RemoveRange(IEnumerable<TEntity> entities)
        => _dbSet.RemoveRange(entities);

    public IAsyncQueryable<TEntity> AsNoTracking()
        => new AsyncQueryable<TEntity>(_dbSet.AsNoTracking(), AsyncExecutor);
}

internal sealed class EfCoreAsyncQueryExecutor : IAsyncQueryExecutor
{
    private static readonly MethodInfo ApplyConstantSetPropertyMethod = typeof(EfCoreAsyncQueryExecutor)
        .GetMethod(nameof(ApplyConstantSetProperty), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ApplyComputedSetPropertyMethod = typeof(EfCoreAsyncQueryExecutor)
        .GetMethod(nameof(ApplyComputedSetProperty), BindingFlags.NonPublic | BindingFlags.Static)!;

    public IAsyncEnumerable<TSource> AsAsyncEnumerable<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => source.AsAsyncEnumerable();

    public Task<int> ExecuteDeleteAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.ExecuteDeleteAsync(source, cancellationToken);

    public Task<int> ExecuteUpdateAsync<TSource>(IQueryable<TSource> source, IUpdateSetters<TSource> setters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setters);

        if (setters.SetPropertyCalls.Count == 0)
        {
            throw new InvalidOperationException("At least one property assignment is required for batch update operations.");
        }

        return EntityFrameworkQueryableExtensions.ExecuteUpdateAsync(
            source,
            efSetters => ApplySetters(efSetters, setters),
            cancellationToken);
    }

    public Task<List<TSource>> ToListAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.ToListAsync(source, cancellationToken);

    public Task<TSource[]> ToArrayAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.ToArrayAsync(source, cancellationToken);

    public Task LoadAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.LoadAsync(source, cancellationToken);

    public Task<bool> AnyAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.AnyAsync(source, cancellationToken);

    public Task<bool> AllAsync<TSource>(IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.AllAsync(source, predicate, cancellationToken);

    public Task<bool> ContainsAsync<TSource>(IQueryable<TSource> source, TSource value, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.ContainsAsync(source, value, cancellationToken);

    public Task<int> CountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.CountAsync(source, cancellationToken);

    public Task<long> LongCountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.LongCountAsync(source, cancellationToken);

    public Task<TSource> FirstAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.FirstAsync(source, cancellationToken);

    public Task<TSource?> FirstOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(source, cancellationToken);

    public Task<TSource> SingleAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.SingleAsync(source, cancellationToken);

    public Task<TSource?> SingleOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(source, cancellationToken);

    public Task<TSource> LastAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.LastAsync(source, cancellationToken);

    public Task<TSource?> LastOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.LastOrDefaultAsync(source, cancellationToken);

    public Task<TSource> ElementAtAsync<TSource>(IQueryable<TSource> source, int index, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.ElementAtAsync(source, index, cancellationToken);

    public Task<TSource?> ElementAtOrDefaultAsync<TSource>(IQueryable<TSource> source, int index, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.ElementAtOrDefaultAsync(source, index, cancellationToken);

    public Task<TSource> MinAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.MinAsync(source, cancellationToken);

    public Task<TSource> MaxAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.MaxAsync(source, cancellationToken);

    public Task<int> SumAsync(IQueryable<int> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(source, cancellationToken);
    public Task<int?> SumAsync(IQueryable<int?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(source, cancellationToken);
    public Task<long> SumAsync(IQueryable<long> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(source, cancellationToken);
    public Task<long?> SumAsync(IQueryable<long?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(source, cancellationToken);
    public Task<float> SumAsync(IQueryable<float> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(source, cancellationToken);
    public Task<float?> SumAsync(IQueryable<float?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(source, cancellationToken);
    public Task<double> SumAsync(IQueryable<double> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(source, cancellationToken);
    public Task<double?> SumAsync(IQueryable<double?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(source, cancellationToken);
    public Task<decimal> SumAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(source, cancellationToken);
    public Task<decimal?> SumAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(source, cancellationToken);

    public Task<double> AverageAsync(IQueryable<int> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(source, cancellationToken);
    public Task<double?> AverageAsync(IQueryable<int?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(source, cancellationToken);
    public Task<double> AverageAsync(IQueryable<long> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(source, cancellationToken);
    public Task<double?> AverageAsync(IQueryable<long?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(source, cancellationToken);
    public Task<float> AverageAsync(IQueryable<float> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(source, cancellationToken);
    public Task<float?> AverageAsync(IQueryable<float?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(source, cancellationToken);
    public Task<double> AverageAsync(IQueryable<double> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(source, cancellationToken);
    public Task<double?> AverageAsync(IQueryable<double?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(source, cancellationToken);
    public Task<decimal> AverageAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(source, cancellationToken);
    public Task<decimal?> AverageAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(source, cancellationToken);

    private static Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<TSource> ApplySetters<TSource>(
        Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<TSource> efSetters,
        IUpdateSetters<TSource> setters)
    {
        foreach (var setPropertyCall in setters.SetPropertyCalls)
        {
            efSetters = ApplySetPropertyCall(efSetters, setPropertyCall);
        }

        return efSetters;
    }

    private static Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<TSource> ApplySetPropertyCall<TSource>(
        Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<TSource> efSetters,
        UpdateSetPropertyCall<TSource> setPropertyCall)
    {
        ArgumentNullException.ThrowIfNull(setPropertyCall);

        var propertyType = setPropertyCall.PropertyExpression.ReturnType;

        return setPropertyCall switch
        {
            ConstantUpdateSetPropertyCall<TSource> constant => (Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<TSource>)ApplyConstantSetPropertyMethod
                .MakeGenericMethod(typeof(TSource), propertyType)
                .Invoke(null, [efSetters, constant.PropertyExpression, constant.Value])!,
            ComputedUpdateSetPropertyCall<TSource> computed => (Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<TSource>)ApplyComputedSetPropertyMethod
                .MakeGenericMethod(typeof(TSource), propertyType)
                .Invoke(null, [efSetters, computed.PropertyExpression, computed.ValueExpression])!,
            _ => throw new NotSupportedException($"Unsupported update setter type '{setPropertyCall.GetType().FullName}'.")
        };
    }

    private static Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<TSource> ApplyConstantSetProperty<TSource, TProperty>(
        Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<TSource> efSetters,
        LambdaExpression propertyExpression,
        object? value)
        => efSetters.SetProperty((Expression<Func<TSource, TProperty>>)propertyExpression, value is null ? default! : (TProperty)value);

    private static Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<TSource> ApplyComputedSetProperty<TSource, TProperty>(
        Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<TSource> efSetters,
        LambdaExpression propertyExpression,
        LambdaExpression valueExpression)
        => efSetters.SetProperty(
            (Expression<Func<TSource, TProperty>>)propertyExpression,
            (Expression<Func<TSource, TProperty>>)valueExpression);
}
