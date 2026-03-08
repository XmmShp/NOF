namespace NOF.Annotation;

/// <summary>
/// Specifies custom prefixes for source-generated code instead of the default <c>AssemblyName</c>.
/// <list type="bullet">
///   <item>The first prefix is used for generated namespace and class names.</item>
///   <item>All prefixes are used for prefix-matching referenced assemblies for scanning.</item>
/// </list>
/// </summary>
/// <example>
/// <code>
/// [assembly: NOF.Annotation.AssemblyPrefix("MyApp")]
/// [assembly: NOF.Annotation.AssemblyPrefix("MyApp", "MyApp.Shared")]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly)]
public class AssemblyPrefixAttribute : Attribute
{
    /// <summary>
    /// The custom prefixes. The first one is used for generated namespace and class names;
    /// all are used for prefix-matching referenced assemblies.
    /// </summary>
    public string[] Prefixes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssemblyPrefixAttribute"/> class.
    /// </summary>
    /// <param name="prefixes">One or more custom prefixes.</param>
    public AssemblyPrefixAttribute(params string[] prefixes)
    {
        Prefixes = prefixes;
    }
}
