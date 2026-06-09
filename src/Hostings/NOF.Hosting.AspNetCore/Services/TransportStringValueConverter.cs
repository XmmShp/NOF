using NOF.Contract;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace NOF.Hosting.AspNetCore;

internal static class TransportStringValueConverter
{
    public static object? Convert(string value, Type propertyType)
    {
        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (targetType == typeof(string))
        {
            return value;
        }

        if (TryParseWithTransportStringParsable(targetType, value, out var parsed))
        {
            return parsed;
        }

        if (TryParseWithStaticTryParse(targetType, value, out parsed))
        {
            return parsed;
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value, ignoreCase: true);
        }

        throw new InvalidOperationException(
            $"Type '{targetType.FullName}' cannot be bound from a transport string. Use a primitive TryParse-compatible type or implement '{typeof(ITransportStringParsable<>).FullName}'.");
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "NOF HTTP endpoint mapping is reflection-based; transport string binding checks the declared parsing interface on request property types.")]
    private static bool TryParseWithTransportStringParsable(Type targetType, string value, out object? parsed)
    {
        parsed = null;
        var implementsContract = targetType.GetInterfaces()
            .Any(i => i.IsGenericType
                && i.GetGenericTypeDefinition() == typeof(ITransportStringParsable<>)
                && i.GetGenericArguments()[0] == targetType);
        if (!implementsContract)
        {
            return false;
        }

        return TryInvokeTryParse(targetType, [typeof(string), typeof(IFormatProvider), targetType.MakeByRefType()], [value, CultureInfo.InvariantCulture, null], out parsed)
            || TryInvokeTryParse(targetType, [typeof(string), targetType.MakeByRefType()], [value, null], out parsed);
    }

    private static bool TryParseWithStaticTryParse(Type targetType, string value, out object? parsed)
    {
        parsed = null;
        return TryInvokeTryParse(targetType, [typeof(string), typeof(IFormatProvider), targetType.MakeByRefType()], [value, CultureInfo.InvariantCulture, null], out parsed)
            || TryInvokeTryParse(targetType, [typeof(string), targetType.MakeByRefType()], [value, null], out parsed);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "NOF HTTP endpoint mapping is reflection-based; transport string binding probes public static TryParse on request property types.")]
    private static bool TryInvokeTryParse(Type targetType, Type[] parameterTypes, object?[] arguments, out object? parsed)
    {
        parsed = null;
        var method = targetType.GetMethod(
            "TryParse",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        if (method is null || method.ReturnType != typeof(bool))
        {
            return false;
        }

        if ((bool)method.Invoke(null, arguments)!)
        {
            parsed = arguments[^1];
            return true;
        }

        throw new FormatException($"Value could not be parsed as '{targetType.FullName}'.");
    }
}
