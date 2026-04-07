using System.ComponentModel;

namespace NOF.Domain;

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
public interface IValueObject<T> where T : notnull
{
    /// <summary>
    /// Validates the primitive value before constructing the value object.
    /// Override this method to add custom validation logic that throws on invalid input.
    /// </summary>
    /// <param name="value">The primitive value to validate.</param>
    static virtual void Validate(T value) { }
}
