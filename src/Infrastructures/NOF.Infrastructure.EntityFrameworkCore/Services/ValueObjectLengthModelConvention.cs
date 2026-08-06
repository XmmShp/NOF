using Microsoft.EntityFrameworkCore.Metadata;
using NOF.Domain;
using System.Reflection;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal static class ValueObjectLengthModelConvention
{
    private static readonly Type _valueObjectInterface = typeof(IValueObject<>);

    public static void Apply(IMutableModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var valueObjectType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (!IsStringValueObject(valueObjectType))
                {
                    continue;
                }

                var lengthAttribute = valueObjectType.GetCustomAttribute<ValueObjectLengthAttribute>(inherit: false);
                if (lengthAttribute is null)
                {
                    continue;
                }

                var configuredLength = property.GetMaxLength();
                if (configuredLength is null)
                {
                    property.SetMaxLength(lengthAttribute.MaximumLength);
                    continue;
                }

                if (configuredLength != lengthAttribute.MaximumLength)
                {
                    throw new InvalidOperationException(
                        $"Property '{entityType.DisplayName()}.{property.Name}' explicitly defines maximum length {configuredLength}, " +
                        $"but value object '{valueObjectType.FullName}' declares maximum length {lengthAttribute.MaximumLength}.");
                }
            }
        }
    }

    private static bool IsStringValueObject(Type type)
        => type.GetInterfaces().Any(@interface =>
            @interface.IsGenericType
            && @interface.GetGenericTypeDefinition() == _valueObjectInterface
            && @interface.GenericTypeArguments[0] == typeof(string));
}
