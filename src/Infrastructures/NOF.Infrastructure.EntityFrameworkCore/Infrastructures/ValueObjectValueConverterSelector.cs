using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NOF.Domain;
using System.Collections.Concurrent;
using System.Reflection;

namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// An EF Core <see cref="IValueConverterSelector"/> that automatically provides
/// <see cref="ValueConverter"/> instances for any type decorated with
/// <c>[ValueObject&lt;TPrimitive&gt;]</c> from NOF.Domain.
/// <para>
/// The value object must expose:
/// <list type="bullet">
///   <item>A static <c>Of(TPrimitive)</c> factory method.</item>
///   <item>An explicit cast operator to <c>TPrimitive</c>.</item>
/// </list>
/// </para>
/// </summary>
internal sealed class ValueObjectValueConverterSelector : ValueConverterSelector
{
    private static readonly ConcurrentDictionary<Type, ValueConverterInfo?> _cache = new();
    private static readonly Type AttributeOpenType = typeof(ValueObjectAttribute<>);

    public ValueObjectValueConverterSelector(ValueConverterSelectorDependencies dependencies)
        : base(dependencies) { }

    public override IEnumerable<ValueConverterInfo> Select(Type modelClrType, Type? providerClrType = null)
    {
        var baseConverters = base.Select(modelClrType, providerClrType);

        var underlyingType = Nullable.GetUnderlyingType(modelClrType) ?? modelClrType;

        var info = _cache.GetOrAdd(underlyingType, BuildConverterInfo);
        if (info is null)
            return baseConverters;

        if (providerClrType is not null && providerClrType != info.Value.ProviderClrType)
            return baseConverters;

        return baseConverters.Prepend(info.Value);
    }

    private static ValueConverterInfo? BuildConverterInfo(Type voType)
    {
        var primitiveType = GetPrimitiveType(voType);
        if (primitiveType is null) return null;

        var ofMethod = voType.GetMethod("Of",
            BindingFlags.Public | BindingFlags.Static,
            null, [primitiveType], null);
        if (ofMethod is null) return null;

        var castMethod = voType.GetMethod("op_Explicit",
            BindingFlags.Public | BindingFlags.Static,
            null, [voType], null);
        if (castMethod is null || castMethod.ReturnType != primitiveType) return null;

        var converterType = typeof(ValueObjectConverter<,>).MakeGenericType(voType, primitiveType);
        var converterInstance = (ValueConverter)Activator.CreateInstance(converterType, ofMethod, castMethod)!;

        return new ValueConverterInfo(
            modelClrType: voType,
            providerClrType: primitiveType,
            factory: _ => converterInstance);
    }

    private static Type? GetPrimitiveType(Type voType)
    {
        foreach (var attr in voType.GetCustomAttributesData())
        {
            if (attr.AttributeType.IsGenericType &&
                attr.AttributeType.GetGenericTypeDefinition() == AttributeOpenType)
                return attr.AttributeType.GenericTypeArguments[0];
        }
        return null;
    }
}

/// <summary>
/// Typed <see cref="ValueConverter{TModel,TProvider}"/> for a value object.
/// </summary>
internal sealed class ValueObjectConverter<TValueObject, TPrimitive> : ValueConverter<TValueObject, TPrimitive>
{
    public ValueObjectConverter(MethodInfo ofMethod, MethodInfo castMethod)
        : base(
            vo => (TPrimitive)castMethod.Invoke(null, new object?[] { vo })!,
            primitive => (TValueObject)ofMethod.Invoke(null, new object?[] { primitive })!)
    {
    }
}
