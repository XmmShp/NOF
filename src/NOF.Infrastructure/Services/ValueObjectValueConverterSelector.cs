using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NOF.Domain;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Infrastructure;

/// <summary>
/// An EF Core <see cref="IValueConverterSelector"/> that automatically provides
/// <see cref="ValueConverter"/> instances for any type implementing
/// <c>IValueObject&lt;TPrimitive&gt;</c> from NOF.Domain.
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
    private static readonly Type _interfaceOpenType = typeof(IValueObject<>);
    private static readonly string _ofMethodName = "Of";

    public ValueObjectValueConverterSelector(ValueConverterSelectorDependencies dependencies)
        : base(dependencies) { }

    public override IEnumerable<ValueConverterInfo> Select(Type modelClrType, Type? providerClrType = null)
    {
        var baseConverters = base.Select(modelClrType, providerClrType);

        var underlyingType = Nullable.GetUnderlyingType(modelClrType) ?? modelClrType;

        var info = _cache.GetOrAdd(underlyingType, BuildConverterInfo);
        if (info is null
            || (providerClrType is not null
                && providerClrType != info.Value.ProviderClrType))
        {
            return baseConverters;
        }

        return baseConverters.Prepend(info.Value);
    }

    [RequiresDynamicCode("Calls System.Type.MakeGenericType(params Type[])")]
    private static ValueConverterInfo? BuildConverterInfo([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.Interfaces)] Type voType)
    {
        var primitiveType = GetPrimitiveType(voType);
        if (primitiveType is null)
        {
            return null;
        }

        var ofMethod = voType.GetMethod(_ofMethodName,
            BindingFlags.Public | BindingFlags.Static,
            null, [primitiveType], null);
        if (ofMethod is null)
        {
            return null;
        }

        var castMethod = voType.GetMethod("op_Explicit",
            BindingFlags.Public | BindingFlags.Static,
            null, [voType], null);
        if (castMethod is null || castMethod.ReturnType != primitiveType)
        {
            return null;
        }

        var converterType = typeof(ValueObjectConverter<,>).MakeGenericType(voType, primitiveType);
        var converterInstance = (ValueConverter)Activator.CreateInstance(converterType, ofMethod, castMethod)!;

        return new ValueConverterInfo(
            modelClrType: voType,
            providerClrType: primitiveType,
            factory: _ => converterInstance);
    }

    private static Type? GetPrimitiveType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type voType)
        => voType.GetInterfaces()
            .Where(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == _interfaceOpenType)
            .Select(iface => iface.GenericTypeArguments[0])
            .FirstOrDefault();
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
