using NOF.Annotation;

namespace NOF.Domain;

/// <summary>
/// Attribute for defining failure entries that are auto-generated as static instances by the source generator.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class FailureAttribute : AttributeForSourceGenerator
{
    /// <summary>
    /// The failure name (used as the static field name).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The failure message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// The failure error code.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FailureAttribute"/> class.
    /// </summary>
    /// <param name="name">The failure name.</param>
    /// <param name="message">The failure message.</param>
    /// <param name="errorCode">The failure error code.</param>
    public FailureAttribute(string name, string message, string errorCode)
    {
        Name = name;
        Message = message;
        ErrorCode = errorCode;
    }
}
