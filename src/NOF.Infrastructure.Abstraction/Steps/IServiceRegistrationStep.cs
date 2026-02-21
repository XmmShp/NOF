namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Defines a service-level configuration unit that participates in the DI container registration phase.
/// Implementations are executed early in the application lifecycle, before the host is built,
/// and may register services, configure options, or set up infrastructure components.
/// </summary>
public interface IServiceRegistrationStep : IStep
{
    /// <summary>
    /// Asynchronously executes the service configuration logic using the provided registration context.
    /// This method is called during the service registration stage and should not perform I/O-bound
    /// operations that block host startup unless necessary.
    /// <para>
    /// The <paramref name="builder"/> is an <see cref="IServiceRegistrationContext"/> which allows
    /// adding initialization steps but NOT additional registration steps.
    /// </para>
    /// </summary>
    /// <param name="builder">The registration context used to access services, configuration, and extension points.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask ExecuteAsync(IServiceRegistrationContext builder);
}
