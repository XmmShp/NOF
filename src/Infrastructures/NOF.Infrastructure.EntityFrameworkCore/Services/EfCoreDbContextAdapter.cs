using Microsoft.EntityFrameworkCore;
using NOF.Application;
using System.Linq.Expressions;
using System.Reflection;
using AppDbException = NOF.Application.DbException;
using AppDbTransactionCommitException = NOF.Application.DbTransactionCommitException;
using AppDbTransactionException = NOF.Application.DbTransactionException;
using AppDbUpdateConcurrencyException = NOF.Application.DbUpdateConcurrencyException;
using AppDbUpdateException = NOF.Application.DbUpdateException;
using EfDbContextTransaction = Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction;
using EfDbUpdateConcurrencyException = Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException;
using EfDbUpdateException = Microsoft.EntityFrameworkCore.DbUpdateException;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal sealed class EfCoreDbContextAdapter(DbContext dbContext) : IDbContext
{
    private readonly DbContext _dbContext = dbContext;
    private readonly EfCoreAsyncQueryExecutor _asyncExecutor = new();

    public IDbSet<TEntity> Set<TEntity>()
        where TEntity : class
        => new EfCoreDbSetAdapter<TEntity>(_dbContext.Set<TEntity>(), _asyncExecutor);

    public int SaveChanges()
        => SaveChanges(acceptAllChangesOnSuccess: true);

    public int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try
        {
            return _dbContext.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (Exception ex)
        {
            throw EfCoreExceptionTranslator.TranslateSaveChangesException(ex);
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (Exception ex)
        {
            throw EfCoreExceptionTranslator.TranslateSaveChangesException(ex);
        }
    }

    public IDbContextTransaction BeginTransaction()
    {
        try
        {
            return new EfCoreDbContextTransactionAdapter(_dbContext.Database.BeginTransaction());
        }
        catch (Exception ex)
        {
            throw EfCoreExceptionTranslator.TranslateTransactionException(
                ex,
                "Failed to begin a database transaction.");
        }
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return new EfCoreDbContextTransactionAdapter(await _dbContext.Database.BeginTransactionAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            throw EfCoreExceptionTranslator.TranslateTransactionException(
                ex,
                "Failed to begin a database transaction.");
        }
    }
}

internal sealed class EfCoreDbContextTransactionAdapter(EfDbContextTransaction transaction) : IDbContextTransaction
{
    private readonly EfDbContextTransaction _transaction = transaction;

    public Guid TransactionId => _transaction.TransactionId;

    public void Commit()
    {
        try
        {
            _transaction.Commit();
        }
        catch (Exception ex)
        {
            throw EfCoreExceptionTranslator.TranslateCommitException(ex);
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw EfCoreExceptionTranslator.TranslateCommitException(ex);
        }
    }

    public void Rollback()
    {
        try
        {
            _transaction.Rollback();
        }
        catch (Exception ex)
        {
            throw EfCoreExceptionTranslator.TranslateTransactionException(
                ex,
                "Failed to roll back the database transaction.");
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw EfCoreExceptionTranslator.TranslateTransactionException(
                ex,
                "Failed to roll back the database transaction.");
        }
    }

    public void CreateSavepoint(string name)
    {
        try
        {
            _transaction.CreateSavepoint(name);
        }
        catch (Exception ex)
        {
            throw EfCoreExceptionTranslator.TranslateTransactionException(
                ex,
                $"Failed to create transaction savepoint '{name}'.");
        }
    }

    public async Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            await _transaction.CreateSavepointAsync(name, cancellationToken);
        }
        catch (Exception ex)
        {
            throw EfCoreExceptionTranslator.TranslateTransactionException(
                ex,
                $"Failed to create transaction savepoint '{name}'.");
        }
    }

    public void RollbackToSavepoint(string name)
    {
        try
        {
            _transaction.RollbackToSavepoint(name);
        }
        catch (Exception ex)
        {
            throw EfCoreExceptionTranslator.TranslateTransactionException(
                ex,
                $"Failed to roll back to transaction savepoint '{name}'.");
        }
    }

    public async Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            await _transaction.RollbackToSavepointAsync(name, cancellationToken);
        }
        catch (Exception ex)
        {
            throw EfCoreExceptionTranslator.TranslateTransactionException(
                ex,
                $"Failed to roll back to transaction savepoint '{name}'.");
        }
    }

    public void ReleaseSavepoint(string name)
    {
        try
        {
            _transaction.ReleaseSavepoint(name);
        }
        catch (Exception ex)
        {
            throw EfCoreExceptionTranslator.TranslateTransactionException(
                ex,
                $"Failed to release transaction savepoint '{name}'.");
        }
    }

    public async Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            await _transaction.ReleaseSavepointAsync(name, cancellationToken);
        }
        catch (Exception ex)
        {
            throw EfCoreExceptionTranslator.TranslateTransactionException(
                ex,
                $"Failed to release transaction savepoint '{name}'.");
        }
    }

    public void Dispose()
        => _transaction.Dispose();

    public ValueTask DisposeAsync()
        => _transaction.DisposeAsync();
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

    private static IQueryable<TSource> Unwrap<TSource>(IQueryable<TSource> source)
        => source is IAsyncQueryableAccessor accessor
            ? (IQueryable<TSource>)accessor.Query
            : source;

    public IAsyncEnumerable<TSource> AsAsyncEnumerable<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => Unwrap(source).AsAsyncEnumerable();

    public Task<int> ExecuteDeleteAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.ExecuteDeleteAsync(Unwrap(source), cancellationToken);

    public Task<int> ExecuteUpdateAsync<TSource>(IQueryable<TSource> source, IUpdateSetters<TSource> setters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setters);

        if (setters.SetPropertyCalls.Count == 0)
        {
            throw new InvalidOperationException("At least one property assignment is required for batch update operations.");
        }

        return EntityFrameworkQueryableExtensions.ExecuteUpdateAsync(
            Unwrap(source),
            efSetters => ApplySetters(efSetters, setters),
            cancellationToken);
    }

    public Task<List<TSource>> ToListAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.ToListAsync(Unwrap(source), cancellationToken);

    public Task<TSource[]> ToArrayAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.ToArrayAsync(Unwrap(source), cancellationToken);

    public Task LoadAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.LoadAsync(Unwrap(source), cancellationToken);

    public Task<bool> AnyAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.AnyAsync(Unwrap(source), cancellationToken);

    public Task<bool> AllAsync<TSource>(IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.AllAsync(Unwrap(source), predicate, cancellationToken);

    public Task<bool> ContainsAsync<TSource>(IQueryable<TSource> source, TSource value, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.ContainsAsync(Unwrap(source), value, cancellationToken);

    public Task<int> CountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.CountAsync(Unwrap(source), cancellationToken);

    public Task<long> LongCountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.LongCountAsync(Unwrap(source), cancellationToken);

    public Task<TSource> FirstAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.FirstAsync(Unwrap(source), cancellationToken);

    public Task<TSource?> FirstOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(Unwrap(source), cancellationToken);

    public Task<TSource> SingleAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.SingleAsync(Unwrap(source), cancellationToken);

    public Task<TSource?> SingleOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(Unwrap(source), cancellationToken);

    public Task<TSource> LastAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.LastAsync(Unwrap(source), cancellationToken);

    public Task<TSource?> LastOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.LastOrDefaultAsync(Unwrap(source), cancellationToken);

    public Task<TSource> ElementAtAsync<TSource>(IQueryable<TSource> source, int index, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.ElementAtAsync(Unwrap(source), index, cancellationToken);

    public Task<TSource?> ElementAtOrDefaultAsync<TSource>(IQueryable<TSource> source, int index, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.ElementAtOrDefaultAsync(Unwrap(source), index, cancellationToken);

    public Task<TSource> MinAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.MinAsync(Unwrap(source), cancellationToken);

    public Task<TSource> MaxAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.MaxAsync(Unwrap(source), cancellationToken);

    public Task<int> SumAsync(IQueryable<int> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(Unwrap(source), cancellationToken);
    public Task<int?> SumAsync(IQueryable<int?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(Unwrap(source), cancellationToken);
    public Task<long> SumAsync(IQueryable<long> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(Unwrap(source), cancellationToken);
    public Task<long?> SumAsync(IQueryable<long?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(Unwrap(source), cancellationToken);
    public Task<float> SumAsync(IQueryable<float> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(Unwrap(source), cancellationToken);
    public Task<float?> SumAsync(IQueryable<float?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(Unwrap(source), cancellationToken);
    public Task<double> SumAsync(IQueryable<double> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(Unwrap(source), cancellationToken);
    public Task<double?> SumAsync(IQueryable<double?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(Unwrap(source), cancellationToken);
    public Task<decimal> SumAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(Unwrap(source), cancellationToken);
    public Task<decimal?> SumAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.SumAsync(Unwrap(source), cancellationToken);

    public Task<double> AverageAsync(IQueryable<int> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(Unwrap(source), cancellationToken);
    public Task<double?> AverageAsync(IQueryable<int?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(Unwrap(source), cancellationToken);
    public Task<double> AverageAsync(IQueryable<long> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(Unwrap(source), cancellationToken);
    public Task<double?> AverageAsync(IQueryable<long?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(Unwrap(source), cancellationToken);
    public Task<float> AverageAsync(IQueryable<float> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(Unwrap(source), cancellationToken);
    public Task<float?> AverageAsync(IQueryable<float?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(Unwrap(source), cancellationToken);
    public Task<double> AverageAsync(IQueryable<double> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(Unwrap(source), cancellationToken);
    public Task<double?> AverageAsync(IQueryable<double?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(Unwrap(source), cancellationToken);
    public Task<decimal> AverageAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(Unwrap(source), cancellationToken);
    public Task<decimal?> AverageAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default) => EntityFrameworkQueryableExtensions.AverageAsync(Unwrap(source), cancellationToken);

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

internal static class EfCoreExceptionTranslator
{
    public static Exception TranslateSaveChangesException(Exception exception)
        => exception switch
        {
            AppDbException => exception,
            EfDbUpdateConcurrencyException ex => new AppDbUpdateConcurrencyException(
                "A concurrency violation was detected while saving changes.",
                ex),
            EfDbUpdateException ex => new AppDbUpdateException(
                "An error occurred while saving changes to the database.",
                ex),
            _ => exception
        };

    public static AppDbTransactionException TranslateTransactionException(Exception exception, string message)
        => exception switch
        {
            AppDbTransactionException ex => ex,
            _ => new AppDbTransactionException(message, exception)
        };

    public static AppDbTransactionCommitException TranslateCommitException(Exception exception)
        => exception switch
        {
            AppDbTransactionCommitException ex => ex,
            _ => new AppDbTransactionCommitException(
                "Failed to commit the database transaction.",
                exception)
        };
}
