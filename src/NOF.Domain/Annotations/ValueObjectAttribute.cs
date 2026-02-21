namespace NOF.Domain;

/// <summary>
/// Marks a <c>readonly partial struct</c> as a value object.
/// The source generator will produce:
/// <list type="bullet">
///   <item>A private constructor accepting the primitive value.</item>
///   <item>A static <c>Of(TPrimitive)</c> factory method that validates and returns the value object.</item>
///   <item>An explicit cast operator from <typeparamref name="TPrimitive"/> to the value object.</item>
///   <item>An explicit cast operator from the value object to <typeparamref name="TPrimitive"/>.</item>
///   <item>
///     Validation via <see cref="System.ComponentModel.DataAnnotations.ValidationAttribute"/>s placed on
///     the parameter of an optional <c>partial void Validate(<typeparamref name="TPrimitive"/> value)</c>
///     method, plus the body of that method — both throwing <see cref="DomainException"/> on failure.
///   </item>
///   <item>A nested <c>JsonConverter</c> and a <c>[JsonConverter]</c> attribute on the struct.</item>
///   <item><c>Equals</c>, <c>GetHashCode</c>, and <c>ToString</c> delegating to the primitive.</item>
/// </list>
/// The <c>Value</c> property is intentionally <b>not</b> exposed; retrieve the primitive via explicit cast.
/// </summary>
/// <typeparam name="TPrimitive">The underlying primitive type (e.g. <c>string</c>, <c>int</c>, <c>Guid</c>).</typeparam>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class ValueObjectAttribute<TPrimitive> : Attribute;
