using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Linq.Expressions;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal interface IEfCoreModelBuilderAccessor
{
    ModelBuilder ModelBuilder { get; }
}

internal sealed class EfCoreModelBuilderAdapter(ModelBuilder modelBuilder) : IDbModelBuilder, IEfCoreModelBuilderAccessor
{
    public ModelBuilder ModelBuilder { get; } = modelBuilder;

    public void Entity<TEntity>(Action<IDbEntityTypeBuilder<TEntity>> configure)
        where TEntity : class
    {
        ModelBuilder.Entity<TEntity>(entityBuilder =>
        {
            configure(new EfCoreEntityTypeBuilderAdapter<TEntity>(entityBuilder));
        });
    }
}

internal sealed class EfCoreEntityTypeBuilderAdapter<TEntity>(EntityTypeBuilder<TEntity> entityBuilder)
    : IDbEntityTypeBuilder<TEntity>
    where TEntity : class
{
    private readonly EntityTypeBuilder<TEntity> _entityBuilder = entityBuilder;

    public IDbEntityTypeBuilder<TEntity> ToTable(string name)
    {
        _entityBuilder.ToTable(name);
        return this;
    }

    public IDbEntityTypeBuilder<TEntity> IsHostOnly()
    {
        _entityBuilder.IsHostOnly();
        return this;
    }

    public IDbEntityTypeBuilder<TEntity> HasKey<TProperty>(Expression<Func<TEntity, TProperty>> keyExpression)
    {
        _entityBuilder.HasKey(ToObjectExpression(keyExpression));
        return this;
    }

    public IDbEntityTypeBuilder<TEntity> HasIndex<TProperty>(Expression<Func<TEntity, TProperty>> indexExpression)
    {
        _entityBuilder.HasIndex(ToObjectExpression(indexExpression));
        return this;
    }

    public IDbPropertyBuilder<TEntity, TProperty> Property<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
        => new EfCorePropertyBuilderAdapter<TEntity, TProperty>(_entityBuilder.Property(propertyExpression));

    private static Expression<Func<TEntity, object?>> ToObjectExpression<TProperty>(Expression<Func<TEntity, TProperty>> expression)
    {
        var body = expression.Body.Type.IsValueType
            ? Expression.Convert(expression.Body, typeof(object))
            : expression.Body;

        return Expression.Lambda<Func<TEntity, object?>>(body, expression.Parameters);
    }
}

internal sealed class EfCorePropertyBuilderAdapter<TEntity, TProperty>(PropertyBuilder<TProperty> propertyBuilder)
    : IDbPropertyBuilder<TEntity, TProperty>
    where TEntity : class
{
    private readonly PropertyBuilder<TProperty> _propertyBuilder = propertyBuilder;

    public IDbPropertyBuilder<TEntity, TProperty> HasMaxLength(int maxLength)
    {
        _propertyBuilder.HasMaxLength(maxLength);
        return this;
    }

    public IDbPropertyBuilder<TEntity, TProperty> IsRequired()
    {
        _propertyBuilder.IsRequired();
        return this;
    }
}
