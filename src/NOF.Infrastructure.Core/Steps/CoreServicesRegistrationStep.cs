using Microsoft.Extensions.DependencyInjection;
using NOF.Infrastructure.Abstraction;
using NOF.Infrastructure.Memory;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers core framework services including invocation context, command sender,
/// notification publisher, handler resolvers, and handler endpoint name map.
/// </summary>
public class CoreServicesRegistrationStep : IBaseSettingsServiceRegistrationStep<CoreServicesRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.AddHostedService<MemoryPersistenceWarningHostedService>();
        builder.Services.AddHostedService<OutboxMessageBackgroundService>();
        builder.Services.AddOptions<OutboxOptions>();
        return ValueTask.CompletedTask;
    }
}
