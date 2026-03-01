using System.ComponentModel;

namespace NOF.Domain;

/// <summary>
/// Non-generic marker interface for value objects.
/// <para>Do not implement directly — implement <see cref="IValueObject{T}"/> instead.
/// The source generator will produce all boilerplate.</para>
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IValueObject
{
    /// <summary>
    /// Returns the underlying primitive value as an <see cref="object"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    object GetUnderlyingValue();
}

/// <summary>
/// Marks a <c>readonly partial struct</c> as a value object wrapping <typeparamref name="T"/>.
/// The source generator will produce:
/// <list type="bullet">
///   <item>A private constructor accepting the primitive value.</item>
///   <item>A static <c>Of(T)</c> factory method that validates and returns the value object.</item>
///   <item>An explicit cast operator from the value object to <typeparamref name="T"/>.</item>
///   <item>A nested <c>JsonConverter</c> and a <c>[JsonConverter]</c> attribute on the struct.</item>
///   <item><c>Equals</c>, <c>GetHashCode</c>, and <c>ToString</c> delegating to the primitive.</item>
/// </list>
/// <para>Override <see cref="Validate"/> to add custom validation logic.</para>
/// </summary>
/// <typeparam name="T">The underlying primitive type (e.g. <c>string</c>, <c>int</c>, <c>Guid</c>).</typeparam>
public interface IValueObject<T> : IValueObject
{
    /// <summary>
    /// Returns the underlying primitive value.
    /// </summary>
    new T GetUnderlyingValue();

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    object IValueObject.GetUnderlyingValue() => GetUnderlyingValue()!;

    /// <summary>
    /// Returns the <see cref="System.Type"/> of the underlying primitive.
    /// </summary>
    static Type GetUnderlyingType() => typeof(T);

    /// <summary>
    /// Validates the primitive value before constructing the value object.
    /// Override this method to add custom validation logic that throws on invalid input.
    /// </summary>
    /// <param name="value">The primitive value to validate.</param>
    static virtual void Validate(T value) { }
}
