using System.Collections.Frozen;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace System;

public static class EnumExtensions
{
    extension<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        public string ToDisplayString()
        {
            var valueString = value.ToString();

            if (EnumMetadataCache<TEnum>.DisplayAttributes.TryGetValue(valueString, out var attribute))
            {
                return attribute?.GetName() ?? valueString;
            }

            if (!EnumMetadataCache<TEnum>.IsFlags || !valueString.Contains(", ", StringComparison.Ordinal))
            {
                return valueString;
            }

            return string.Join(", ", valueString.Split(", ", StringSplitOptions.None).Select(static name =>
                EnumMetadataCache<TEnum>.DisplayAttributes.TryGetValue(name, out var displayAttribute)
                    ? displayAttribute?.GetName() ?? name
                    : name));
        }
    }

    private static class EnumMetadataCache<TEnum>
        where TEnum : struct, Enum
    {
        public static readonly bool IsFlags = typeof(TEnum).IsDefined(typeof(FlagsAttribute), inherit: false);

        public static readonly FrozenDictionary<string, DisplayAttribute?> DisplayAttributes = typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .ToFrozenDictionary(
                static field => field.Name,
                static field => field.GetCustomAttribute<DisplayAttribute>());
    }
}
