using NOF.Annotation;

namespace NOF.Contract;

/// <summary>
/// Specifies a summary for the endpoint, used to enhance OpenAPI documentation.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SummaryAttribute : MetadataAttribute
{
    public const string MetadataKey = "api.summary";

    /// <summary>
    /// Creates a new SummaryAttribute instance
    /// </summary>
    /// <param name="summary">The summary text for the endpoint</param>
    public SummaryAttribute(string summary)
        : base(MetadataKey, summary)
    {
    }

    /// <summary>
    /// The summary text
    /// </summary>
    public string Summary => Value;
}
