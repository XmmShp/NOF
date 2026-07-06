namespace NOF.Domain;

/// <summary>
/// When applied to an <c>IValueObject&lt;long&gt;</c> struct, instructs the source generator
/// to emit static <c>New()</c> and <c>New(IIdGenerator)</c> factory methods.
/// </summary>
/// <remarks>
/// Only valid on <c>IValueObject&lt;long&gt;</c> structs. Applying it to a struct with any other
/// primitive type will produce a compile-time error (NOF012).
/// The generated <c>New()</c> factory is a convenience API that uses the ambient
/// <see cref="IdGenerator"/>. Use <c>New(IIdGenerator)</c> when you want an explicit dependency.
/// </remarks>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class NewableValueObjectAttribute : Attribute;
