using Microsoft.Extensions.DependencyInjection;

namespace NOF.Abstraction;

/// <summary>
/// Represents an assembly-level runtime initialization hook.
/// </summary>
public abstract class AssemblyInitializeAttribute : Attribute
{
    /// <summary>
    /// Runs the assembly initializer against the provided service collection.
    /// </summary>
    public abstract void Initialize(IServiceCollection services);
}

/// <summary>
/// Registers a static assembly initializer type.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AssemblyInitializeAttribute<TInitializer> : AssemblyInitializeAttribute
    where TInitializer : IAssemblyInitializer
{
    public override void Initialize(IServiceCollection services)
    {
        TInitializer.Initialize(services);
    }
}
