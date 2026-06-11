namespace NOF.Hosting;

/// <summary>
/// Registers services from source-generated AutoInject metadata.
/// </summary>
public sealed class AutoInjectServiceRegistrationStep : IServiceRegistrationStep
{
    public TopologyComparison Compare(IServiceRegistrationStep other) => TopologyComparison.DoesNotMatter;

    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        foreach (var descriptor in builder.Registry.AutoInjectRegistry.Freeze())
        {
            builder.Services.Add(descriptor);
        }

        return ValueTask.CompletedTask;
    }
}
