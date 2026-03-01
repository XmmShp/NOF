namespace NOF.Application;

/// <summary>
/// Provides access to the global <see cref="IMapper"/> singleton.
/// Must be initialized via <c>Mapper.SetCurrent</c> before first use
/// (typically done by an application initialization step).
/// </summary>
public static class Mapper
{
    private static IMapper? _current;

    /// <summary>
    /// Gets the globally registered <see cref="IMapper"/> instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="SetCurrent"/> has not been called yet.
    /// </exception>
    public static IMapper Current
        => _current ?? throw new InvalidOperationException(
            "Mapper has not been initialized. " +
            "Register mappings via ManualMapper during application setup.");

    /// <summary>
    /// Sets the global <see cref="IMapper"/> instance.
    /// Should be called once during application startup.
    /// </summary>
    public static void SetCurrent(IMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        _current = mapper;
    }
}
