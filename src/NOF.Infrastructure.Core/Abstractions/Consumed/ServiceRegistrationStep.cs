namespace NOF.Infrastructure.Core;

/// <summary>
/// Defines a service-level configuration unit that participates in the DI container registration phase.
/// Implementations are executed early in the application lifecycle, before the host is built,
/// and may register services, configure options, or set up infrastructure components.
/// </summary>
public interface IServiceRegistrationStep : IStep
{
    /// <summary>
    /// Asynchronously executes the service configuration logic using the provided application builder.
    /// This method is called during the service registration stage and should not perform I/O-bound
    /// operations that block host startup unless necessary.
    /// </summary>
    /// <param name="builder">The application builder used to access services, configuration, and extension points.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask ExecuteAsync(INOFAppBuilder builder);
}

public class ServiceRegistrationStep : IServiceRegistrationStep
{
    private readonly Func<INOFAppBuilder, ValueTask> _configurator;

    public ServiceRegistrationStep(Func<INOFAppBuilder, ValueTask> configurator)
    {
        _configurator = configurator;
    }
    public ValueTask ExecuteAsync(INOFAppBuilder builder)
    {
        return _configurator(builder);
    }
}
