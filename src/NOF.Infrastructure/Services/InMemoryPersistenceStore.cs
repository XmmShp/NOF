using NOF.Application;
using NOF.Domain;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace NOF.Infrastructure;

[RequiresUnreferencedCode("The in-memory persistence store snapshots arbitrary entity types via reflection and is intended for tests/development, not trimmed applications.")]
[RequiresDynamicCode("The in-memory persistence store compiles update expressions and is intended for tests/development, not Native AOT.")]
public sealed class InMemoryPersistenceStore
{
    private readonly object _gate = new();
    private Dictionary<Type, List<object>> _tables = [];

    internal List<TEntity> Snapshot<TEntity>()
        where TEntity : class
    {
        lock (_gate)
        {
            return GetTable(typeof(TEntity))
                .Select(static entity => (TEntity)CloneEntity(entity))
                .ToList();
        }
    }

    internal int Save(IReadOnlyCollection<InMemoryPersistenceChange> changes)
    {
        lock (_gate)
        {
            foreach (var change in changes)
            {
                var table = GetTable(change.EntityType);
                switch (change.Kind)
                {
                    case InMemoryPersistenceChangeKind.Add:
                        EnsureNotExists(table, change.Entity);
                        table.Add(CloneEntity(change.Entity));
                        break;
                    case InMemoryPersistenceChangeKind.Update:
                        ReplaceOrAdd(table, change.Entity);
                        break;
                    case InMemoryPersistenceChangeKind.Remove:
                        table.RemoveAll(item => ReferenceEquals(item, change.Entity) || SameKey(item, change.Entity));
                        break;
                }
            }

            return changes.Count;
        }
    }

    internal int ExecuteDelete<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class
    {
        lock (_gate)
        {
            var table = GetTable(typeof(TEntity));
            var selected = entities.Cast<object>().ToList();
            return table.RemoveAll(item => selected.Any(selectedItem => ReferenceEquals(item, selectedItem) || SameKey(item, selectedItem)));
        }
    }

    internal int ExecuteUpdate<TEntity>(IEnumerable<TEntity> entities, IUpdateSetters<TEntity> setters)
        where TEntity : class
    {
        lock (_gate)
        {
            var table = GetTable(typeof(TEntity));
            var selected = entities.Cast<object>().ToList();
            var updated = 0;
            foreach (var entity in table.Cast<TEntity>().Where(entity => selected.Any(selectedItem => SameKey(entity, selectedItem))))
            {
                foreach (var setter in setters.SetPropertyCalls)
                {
                    ApplySetter(entity, setter);
                }

                updated++;
            }

            return updated;
        }
    }

    internal InMemoryPersistenceSnapshot CaptureSnapshot()
    {
        lock (_gate)
        {
            return new InMemoryPersistenceSnapshot(CloneTables(_tables));
        }
    }

    internal void RestoreSnapshot(InMemoryPersistenceSnapshot snapshot)
    {
        lock (_gate)
        {
            _tables = CloneTables(snapshot.Tables);
        }
    }

    private List<object> GetTable(Type entityType)
    {
        if (!_tables.TryGetValue(entityType, out var table))
        {
            table = [];
            _tables[entityType] = table;
        }

        return table;
    }

    private static void EnsureNotExists(List<object> table, object entity)
    {
        if (table.Any(item => SameKey(item, entity)))
        {
            throw new DbUpdateException("An entity with the same key already exists in the in-memory persistence store.");
        }
    }

    private static void ReplaceOrAdd(List<object> table, object entity)
    {
        var index = table.FindIndex(item => ReferenceEquals(item, entity) || SameKey(item, entity));
        if (index < 0)
        {
            table.Add(entity);
            return;
        }

        table[index] = entity;
    }

    internal static bool SameKey(object left, object right)
    {
        if (left.GetType() != right.GetType())
        {
            return false;
        }

        var keyValues = GetKeyValues(left);
        if (keyValues.Count == 0)
        {
            return false;
        }

        return keyValues.SequenceEqual(GetKeyValues(right));
    }

    private static IReadOnlyList<object?> GetKeyValues(object entity)
    {
        if (entity is NOFInboxMessage inboxMessage)
        {
            return [inboxMessage.Id, inboxMessage.Route];
        }

        var idProperty = entity.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return idProperty is null ? [] : [idProperty.GetValue(entity)];
    }

    private static void ApplySetter<TEntity>(TEntity entity, UpdateSetPropertyCall<TEntity> setter)
        where TEntity : class
    {
        var member = GetAssignedMember(setter.PropertyExpression);
        var value = setter switch
        {
            ConstantUpdateSetPropertyCall<TEntity> constant => constant.Value,
            ComputedUpdateSetPropertyCall<TEntity> computed => computed.ValueExpression.Compile().DynamicInvoke(entity),
            _ => throw new NotSupportedException($"Unsupported update setter type '{setter.GetType().FullName}'.")
        };

        switch (member)
        {
            case PropertyInfo property:
                property.SetValue(entity, value);
                break;
            case FieldInfo field:
                field.SetValue(entity, value);
                break;
            default:
                throw new NotSupportedException($"Unsupported update member '{member.MemberType}'.");
        }
    }

    private static MemberInfo GetAssignedMember(LambdaExpression expression)
    {
        var body = expression.Body is UnaryExpression unaryExpression
            ? unaryExpression.Operand
            : expression.Body;

        return body is MemberExpression memberExpression
            ? memberExpression.Member
            : throw new NotSupportedException("Batch update expressions must target a field or property.");
    }

    private static Dictionary<Type, List<object>> CloneTables(Dictionary<Type, List<object>> tables)
        => tables.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Select(CloneEntity).ToList());

    internal static bool SameValues(object left, object right)
    {
        if (left.GetType() != right.GetType())
        {
            return false;
        }

        foreach (var property in left.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .Where(static property => property.CanRead && property.GetIndexParameters().Length == 0))
        {
            if (!Equals(property.GetValue(left), property.GetValue(right)))
            {
                return false;
            }
        }

        foreach (var field in left.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!Equals(field.GetValue(left), field.GetValue(right)))
            {
                return false;
            }
        }

        return true;
    }

    internal static object CloneEntity(object entity)
    {
        if (entity is ICloneable cloneable)
        {
            return cloneable.Clone();
        }

        if (entity.GetType().GetConstructor(Type.EmptyTypes) is null)
        {
            return entity;
        }

        var clone = Activator.CreateInstance(entity.GetType())!;
        foreach (var property in entity.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .Where(static property => property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0))
        {
            property.SetValue(clone, property.GetValue(entity));
        }

        foreach (var field in entity.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            field.SetValue(clone, field.GetValue(entity));
        }

        return clone;
    }
}

internal sealed record InMemoryPersistenceSnapshot(Dictionary<Type, List<object>> Tables);

internal sealed record InMemoryPersistenceChange(
    InMemoryPersistenceChangeKind Kind,
    Type EntityType,
    object Entity);

internal enum InMemoryPersistenceChangeKind
{
    Add,
    Update,
    Remove
}
