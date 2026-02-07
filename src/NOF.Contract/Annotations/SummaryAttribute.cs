namespace NOF;

/// <summary>
/// Specifies a summary for the endpoint, used to enhance OpenAPI documentation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class SummaryAttribute : Attribute
{
    /// <summary>
    /// Creates a new SummaryAttribute instance
    /// </summary>
    /// <param name="summary">The summary text for the endpoint</param>
    public SummaryAttribute(string summary)
    {
        Summary = summary;
    }

    /// <summary>
    /// The summary text
    /// </summary>
    public string Summary { get; }
}
