namespace NOF.Application;

/// <summary>
/// Provides access to the global <see cref="IMapper"/> singleton.
/// Must be initialized via <c>Mapper.SetCurrent</c> before first use
/// (typically done by an application initialization step).
/// </summary>
public static class Mapper
{
    /// <summary>
    /// Gets the globally registered <see cref="IMapper"/> instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="SetCurrent"/> has not been called yet.
    /// </exception>
    public static IMapper Current
    {
        get => field ?? throw new InvalidOperationException(
            "Mapper has not been initialized. " +
            "Ensure application parts are added so assembly initializers run and mappings are registered.");
        private set;
    }

    /// <summary>
    /// Sets the global <see cref="IMapper"/> instance.
    /// Should be called once during application startup.
    /// </summary>
    public static void SetCurrent(IMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        Current = mapper;
    }
}
