namespace NOF.Infrastructure;

/// <summary>
/// Installs <see cref="InitializingServiceProviderFactory"/> so services implementing
/// <c>IInitializable</c> are initialized on first resolve.
/// </summary>
public sealed class InitializingServiceProviderFactoryRegistrationStep : IBaseSettingsServiceRegistrationStep<InitializingServiceProviderFactoryRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.ConfigureContainer(new InitializingServiceProviderFactory());
        return ValueTask.CompletedTask;
    }
}
