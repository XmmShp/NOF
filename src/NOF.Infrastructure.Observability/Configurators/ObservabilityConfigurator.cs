namespace NOF;

public class ObservabilityConfigurator : IConfiguringServicesConfigurator, IObservabilityConfigurator, ICombinedConfigurator
{
    public ValueTask ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.AddObservabilities();
        return ValueTask.CompletedTask;
    }

    public Task ExecuteAsync(StartupArgs args)
    {
        args.App.MapHealthCheckEndpoints();
        return Task.CompletedTask;
    }
}
