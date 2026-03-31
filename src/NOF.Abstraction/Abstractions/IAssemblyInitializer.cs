namespace NOF.Annotation;

/// <summary>
/// Provides a static assembly-level initialization entry point that can be invoked at runtime.
/// </summary>
public interface IAssemblyInitializer
{
    /// <summary>
    /// Initializes assembly-level runtime registrations.
    /// </summary>
    static abstract void Initialize();
}
