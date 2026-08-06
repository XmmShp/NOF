using NOF.Domain;
using System.Reflection;

namespace NOF.Infrastructure.NHibernate;

internal static class NHibernateValueObjectLengthResolver
{
    private static readonly Type _valueObjectInterface = typeof(IValueObject<>);

    public static int? Resolve(Type entityType, PropertyInfo propertyInfo, int? configuredLength)
    {
        var valueObjectType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
        if (!IsStringValueObject(valueObjectType))
        {
            return configuredLength;
        }

        var lengthAttribute = valueObjectType.GetCustomAttribute<ValueObjectLengthAttribute>(inherit: false);
        if (lengthAttribute is null)
        {
            return configuredLength;
        }

        if (configuredLength is not null && configuredLength != lengthAttribute.MaximumLength)
        {
            throw new InvalidOperationException(
                $"Property '{entityType.FullName}.{propertyInfo.Name}' explicitly defines maximum length {configuredLength}, " +
                $"but value object '{valueObjectType.FullName}' declares maximum length {lengthAttribute.MaximumLength}.");
        }

        return lengthAttribute.MaximumLength;
    }

    private static bool IsStringValueObject(Type type)
        => type.GetInterfaces().Any(@interface =>
            @interface.IsGenericType
            && @interface.GetGenericTypeDefinition() == _valueObjectInterface
            && @interface.GenericTypeArguments[0] == typeof(string));
}
