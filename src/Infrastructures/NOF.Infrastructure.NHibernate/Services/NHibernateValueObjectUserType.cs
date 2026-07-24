using NHibernate.Engine;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;

namespace NOF.Infrastructure.NHibernate;

internal sealed class NHibernateValueObjectUserType<TValueObject, TPrimitive> : IUserType
    where TValueObject : struct
    where TPrimitive : notnull
{
    private static readonly Func<TPrimitive, TValueObject> Factory = BuildFactory();
    private static readonly Func<TValueObject, TPrimitive> Accessor = BuildAccessor();

    public SqlType[] SqlTypes { get; } = [SqlTypeFactory.GetString(4000)];

    public Type ReturnedType => typeof(TValueObject);

    public new bool Equals(object? x, object? y) => object.Equals(x, y);

    public int GetHashCode(object x) => x.GetHashCode();

    public object? NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor session, object owner)
    {
        var ordinal = rs.GetOrdinal(names[0]);
        if (rs.IsDBNull(ordinal))
        {
            return null;
        }

        var value = (TPrimitive)Convert.ChangeType(rs.GetValue(ordinal), typeof(TPrimitive));
        return Factory(value);
    }

    public void NullSafeSet(DbCommand cmd, object? value, int index, ISessionImplementor session)
    {
        var parameter = (DbParameter)cmd.Parameters[index];
        parameter.Value = value is null
            ? DBNull.Value
            : Accessor((TValueObject)value);
    }

    public object DeepCopy(object value) => value;

    public object Replace(object original, object target, object owner) => original;

    public object Assemble(object cached, object owner) => cached;

    public object Disassemble(object value) => value;

    public bool IsMutable => false;

    private static Func<TPrimitive, TValueObject> BuildFactory()
    {
        var method = typeof(TValueObject).GetMethod(
            "Of",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            [typeof(TPrimitive)],
            modifiers: null);

        if (method is null)
        {
            throw new InvalidOperationException(
                $"Value object '{typeof(TValueObject).FullName}' must expose a public static Of({typeof(TPrimitive).Name}) factory.");
        }

        var value = Expression.Parameter(typeof(TPrimitive), "value");
        return Expression.Lambda<Func<TPrimitive, TValueObject>>(Expression.Call(method, value), value).Compile();
    }

    private static Func<TValueObject, TPrimitive> BuildAccessor()
    {
        var value = Expression.Parameter(typeof(TValueObject), "value");
        return Expression.Lambda<Func<TValueObject, TPrimitive>>(Expression.Convert(value, typeof(TPrimitive)), value).Compile();
    }
}
