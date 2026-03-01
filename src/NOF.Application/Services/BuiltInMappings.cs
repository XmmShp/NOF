using NOF.Contract;
using NOF.Domain;

namespace NOF.Application;

/// <summary>
/// Provides built-in mapping fallbacks for common type conversions.
/// Consulted by <see cref="ManualMapper"/> as a last-resort when no user-registered mapping matches.
/// <para>All conversions are zero-reflection — they use <see langword="static"/> type checks
/// and <see cref="IConvertible"/>/<see cref="IValueObject"/> interfaces.</para>
/// </summary>
internal static class BuiltInMappings
{
    /// <summary>
    /// Attempts a built-in mapping from <paramref name="source"/> (of type <paramref name="sourceType"/>)
    /// to <paramref name="destType"/>. Returns <see cref="Optional.None"/> if no built-in applies.
    /// </summary>
    internal static Optional<object?> TryMap(Type sourceType, Type destType, object source)
    {
        // ── IValueObject → underlying primitive ─────────────────────────
        // Checked first so ValueObject<string> → string extracts the value
        // rather than calling ToString() on the wrapper.
        if (source is IValueObject vo)
        {
            var underlyingValue = vo.GetUnderlyingValue();
            var underlyingType = underlyingValue.GetType();

            // Direct: IValueObject → underlying type
            if (destType.IsAssignableFrom(underlyingType))
            {
                return Optional.Of<object?>(underlyingValue);
            }

            // Chain: IValueObject → underlying → dest (e.g. ValueObject<int> → long)
            var chained = TryMap(underlyingType, destType, underlyingValue);
            if (chained.HasValue)
            {
                return chained;
            }
        }

        // ── Result<T> → T (extract Value if success) ───────────────────
        if (sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var resultType = sourceType.GetGenericArguments()[0];
            if (destType.IsAssignableFrom(resultType))
            {
                var isSuccessProp = sourceType.GetProperty(nameof(Result<object>.IsSuccess))!;
                var valueProp = sourceType.GetProperty(nameof(Result<object>.Value))!;

                if (isSuccessProp.GetValue(source) is true)
                {
                    return Optional.Of<object?>(valueProp.GetValue(source));
                }

                return Optional.None;
            }
        }

        // ── Optional<T> → T (extract Value if has value) ───────────────
        if (sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == typeof(Optional<>))
        {
            var innerType = sourceType.GetGenericArguments()[0];
            if (destType.IsAssignableFrom(innerType))
            {
                var hasValueProp = sourceType.GetProperty(nameof(Optional<object>.HasValue))!;
                var valueProp = sourceType.GetProperty(nameof(Optional<object>.Value))!;

                if (hasValueProp.GetValue(source) is true)
                {
                    return Optional.Of<object?>(valueProp.GetValue(source));
                }

                return Optional.None;
            }
        }

        // ── Numeric conversions via IConvertible ────────────────────────
        if (IsNumericType(destType) && source is IConvertible convertible)
        {
            try
            {
                return Optional.Of<object?>(Convert.ChangeType(convertible, destType));
            }
            catch
            {
                return Optional.None;
            }
        }

        // ── Enum ↔ numeric ──────────────────────────────────────────────
        if (destType.IsEnum && source is IConvertible enumConvertible)
        {
            try
            {
                return Optional.Of<object?>(Enum.ToObject(destType, enumConvertible));
            }
            catch
            {
                return Optional.None;
            }
        }

        if (sourceType.IsEnum && IsNumericType(destType))
        {
            try
            {
                return Optional.Of<object?>(Convert.ChangeType(source, destType));
            }
            catch
            {
                return Optional.None;
            }
        }

        // ── T → string (via ToString) — lowest priority ────────────────
        if (destType == typeof(string))
        {
            return Optional.Of<object?>(source.ToString());
        }

        return Optional.None;
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte)
            || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint)
            || type == typeof(long) || type == typeof(ulong)
            || type == typeof(float) || type == typeof(double)
            || type == typeof(decimal)
            || type == typeof(nint) || type == typeof(nuint);
    }
}
