namespace NOF.Domain;

/// <summary>
/// Provides access to the global <see cref="IIdGenerator"/> singleton.
/// Must be initialized via <c>IdGenerator.SetCurrent</c> before first use
/// (typically done by an application initialization step).
/// </summary>
public static class IdGenerator
{
    /// <summary>
    /// Gets the globally registered <see cref="IIdGenerator"/> instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="SetCurrent"/> has not been called yet.
    /// </exception>
    public static IIdGenerator Current
    {
        get => field ?? throw new InvalidOperationException(
            "IdGenerator has not been initialized. " +
            "Call builder.AddSnowflakeIdGenerator() during application setup.");
        private set;
    }

    /// <summary>
    /// Sets the global <see cref="IIdGenerator"/> instance.
    /// Should be called once during application startup.
    /// </summary>
    public static void SetCurrent(IIdGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        Current = generator;
    }
}
