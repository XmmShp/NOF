using System.ComponentModel;
using System.Linq.Expressions;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IDbModelBuilder
{
    void Entity<TEntity>(Action<IDbEntityTypeBuilder<TEntity>> configure)
        where TEntity : class;
}

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IDbEntityTypeBuilder<TEntity>
    where TEntity : class
{
    IDbEntityTypeBuilder<TEntity> ToTable(string name);

    IDbEntityTypeBuilder<TEntity> IsHostOnly();

    IDbEntityTypeBuilder<TEntity> HasKey<TProperty>(Expression<Func<TEntity, TProperty>> keyExpression);

    IDbEntityTypeBuilder<TEntity> HasKey(params string[] propertyNames);

    IDbIndexBuilder<TEntity> HasIndex<TProperty>(Expression<Func<TEntity, TProperty>> indexExpression);

    IDbIndexBuilder<TEntity> HasIndex(params string[] propertyNames);

    IDbPropertyBuilder<TEntity, TProperty> Property<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IDbIndexBuilder<TEntity>
    where TEntity : class
{
    IDbIndexBuilder<TEntity> IsUnique();
}

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IDbPropertyBuilder<TEntity, TProperty>
    where TEntity : class
{
    IDbPropertyBuilder<TEntity, TProperty> HasMaxLength(int maxLength);

    IDbPropertyBuilder<TEntity, TProperty> IsRequired();
}
