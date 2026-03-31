namespace NOF.Annotation;

/// <summary>
/// Represents an assembly-level runtime initialization hook.
/// </summary>
public abstract class AssemblyInitializeAttribute : Attribute
{
    /// <summary>
    /// Gets the initialization delegate for this assembly initializer entry.
    /// </summary>
    public Action InitializeMethod { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssemblyInitializeAttribute"/> class.
    /// </summary>
    /// <param name="initializeMethod">The initialization delegate.</param>
    protected AssemblyInitializeAttribute(Action initializeMethod)
    {
        ArgumentNullException.ThrowIfNull(initializeMethod);
        InitializeMethod = initializeMethod;
    }
}

/// <summary>
/// Registers a static assembly initializer type.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AssemblyInitializeAttribute<TInitializer> : AssemblyInitializeAttribute
    where TInitializer : IAssemblyInitializer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssemblyInitializeAttribute{TInitializer}"/> class.
    /// </summary>
    public AssemblyInitializeAttribute()
        : base(TInitializer.Initialize)
    {
    }
}
