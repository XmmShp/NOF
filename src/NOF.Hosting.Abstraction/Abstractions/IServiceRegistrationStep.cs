namespace NOF.Hosting;

/// <summary>
/// Defines a service-level configuration unit that participates in the DI container registration phase.
/// </summary>
public interface IServiceRegistrationStep
{
    ValueTask ExecuteAsync(IServiceRegistrationContext builder);
}
