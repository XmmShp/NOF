using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using NOF.Infrastructure;

namespace NOF.Infrastructure.NHibernate;

internal sealed class NHibernateModelDefinitionBuilder : IDbModelBuilder
{
    private readonly ConcurrentDictionary<Type, NHibernateEntityDefinition> _entities = new();

    public IReadOnlyCollection<NHibernateEntityDefinition> Entities => _entities.Values.ToArray();

    public void Entity<TEntity>(Action<IDbEntityTypeBuilder<TEntity>> configure)
        where TEntity : class
    {
        var definition = _entities.GetOrAdd(typeof(TEntity), static type => new NHibernateEntityDefinition(type));
        configure(new NHibernateEntityTypeBuilderAdapter<TEntity>(definition));
    }
}

internal sealed class NHibernateEntityTypeBuilderAdapter<TEntity>(NHibernateEntityDefinition definition)
    : IDbEntityTypeBuilder<TEntity>
    where TEntity : class
{
    private readonly NHibernateEntityDefinition _definition = definition;

    public IDbEntityTypeBuilder<TEntity> ToTable(string name)
    {
        _definition.TableName = name;
        return this;
    }

    public IDbEntityTypeBuilder<TEntity> IsHostOnly()
    {
        _definition.IsHostOnly = true;
        return this;
    }

    public IDbEntityTypeBuilder<TEntity> HasKey<TProperty>(Expression<Func<TEntity, TProperty>> keyExpression)
    {
        _definition.KeyPropertyNames = [.. ExtractPropertyNames(keyExpression)];
        return this;
    }

    public IDbEntityTypeBuilder<TEntity> HasKey(params string[] propertyNames)
    {
        _definition.KeyPropertyNames = [.. propertyNames];
        return this;
    }

    public IDbIndexBuilder<TEntity> HasIndex<TProperty>(Expression<Func<TEntity, TProperty>> indexExpression)
    {
        var index = new NHibernateIndexDefinition(ExtractPropertyNames(indexExpression));
        _definition.Indexes.Add(index);
        return new NHibernateIndexBuilderAdapter<TEntity>(index);
    }

    public IDbIndexBuilder<TEntity> HasIndex(params string[] propertyNames)
    {
        var index = new NHibernateIndexDefinition([.. propertyNames]);
        _definition.Indexes.Add(index);
        return new NHibernateIndexBuilderAdapter<TEntity>(index);
    }

    public IDbPropertyBuilder<TEntity, TProperty> Property<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        var propertyName = ExtractSinglePropertyName(propertyExpression);
        var definition = _definition.Properties.GetOrAdd(propertyName, static name => new NHibernatePropertyDefinition(name));
        return new NHibernatePropertyBuilderAdapter<TEntity, TProperty>(definition);
    }

    private static string ExtractSinglePropertyName<TProperty>(Expression<Func<TEntity, TProperty>> expression)
    {
        var names = ExtractPropertyNames(expression);
        return names.Count == 1
            ? names[0]
            : throw new InvalidOperationException("Expected a single property expression.");
    }

    private static IReadOnlyList<string> ExtractPropertyNames(LambdaExpression expression)
        => expression.Body switch
        {
            MemberExpression member => [member.Member.Name],
            UnaryExpression { Operand: MemberExpression member } => [member.Member.Name],
            NewExpression @new => @new.Arguments.SelectMany(ExtractPropertyNames).Distinct(StringComparer.Ordinal).ToArray(),
            _ => throw new NotSupportedException($"Unsupported property expression '{expression.Body.NodeType}'.")
        };

    private static IEnumerable<string> ExtractPropertyNames(Expression expression)
        => expression switch
        {
            MemberExpression member => [member.Member.Name],
            UnaryExpression { Operand: MemberExpression member } => [member.Member.Name],
            _ => throw new NotSupportedException($"Unsupported property expression '{expression.NodeType}'.")
        };
}

internal sealed class NHibernateIndexBuilderAdapter<TEntity>(NHibernateIndexDefinition definition)
    : IDbIndexBuilder<TEntity>
    where TEntity : class
{
    private readonly NHibernateIndexDefinition _definition = definition;

    public IDbIndexBuilder<TEntity> IsUnique()
    {
        _definition.IsUnique = true;
        return this;
    }
}

internal sealed class NHibernatePropertyBuilderAdapter<TEntity, TProperty>(NHibernatePropertyDefinition definition)
    : IDbPropertyBuilder<TEntity, TProperty>
    where TEntity : class
{
    private readonly NHibernatePropertyDefinition _definition = definition;

    public IDbPropertyBuilder<TEntity, TProperty> HasMaxLength(int maxLength)
    {
        _definition.MaxLength = maxLength;
        return this;
    }

    public IDbPropertyBuilder<TEntity, TProperty> IsRequired()
    {
        _definition.IsRequired = true;
        return this;
    }
}

internal sealed class NHibernateEntityDefinition(Type entityType)
{
    public Type EntityType { get; } = entityType;

    public string? TableName { get; set; }

    public bool IsHostOnly { get; set; }

    public List<string> KeyPropertyNames { get; set; } = [];

    public List<NHibernateIndexDefinition> Indexes { get; } = [];

    public ConcurrentDictionary<string, NHibernatePropertyDefinition> Properties { get; } = new(StringComparer.Ordinal);

    public PropertyInfo GetProperty(string propertyName)
        => EntityType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
           ?? throw new InvalidOperationException($"Property '{propertyName}' was not found on '{EntityType.FullName}'.");
}

internal sealed class NHibernateIndexDefinition(IReadOnlyList<string> propertyNames)
{
    public IReadOnlyList<string> PropertyNames { get; } = propertyNames;

    public bool IsUnique { get; set; }
}

internal sealed class NHibernatePropertyDefinition(string propertyName)
{
    public string PropertyName { get; } = propertyName;

    public int? MaxLength { get; set; }

    public bool IsRequired { get; set; }
}
