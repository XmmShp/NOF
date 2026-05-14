using NOF.Abstraction;

namespace NOF.Annotation;

/// <summary>
/// Represents an assembly-level runtime initialization hook.
/// </summary>
public abstract class AssemblyInitializeAttribute : Attribute
{
    /// <summary>
    /// Runs the assembly initializer against the provided registry.
    /// </summary>
    public abstract void Initialize(Registry registry);
}

/// <summary>
/// Registers a static assembly initializer type.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AssemblyInitializeAttribute<TInitializer> : AssemblyInitializeAttribute
    where TInitializer : IAssemblyInitializer
{
    public override void Initialize(Registry registry)
    {
        TInitializer.Initialize(registry);
    }
}
