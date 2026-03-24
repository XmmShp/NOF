using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure;

/// <summary>
/// Registers core framework services including invocation context, command sender,
/// notification publisher, handler resolvers, and handler endpoint name map.
/// </summary>
public class CoreServicesRegistrationStep : IBaseSettingsServiceRegistrationStep<CoreServicesRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.AddScoped(sp => sp.GetRequiredKeyedService<ICacheService>(ICacheServiceFactory.DefaultName));
        builder.Services.AddScoped<IDistributedCache>(sp => sp.GetRequiredService<ICacheService>());
        builder.Services.AddScoped<IInvocationContext>(sp => sp.GetRequiredService<IMutableInvocationContext>());
        builder.Services.AddScoped<IUserContext>(sp => sp.GetRequiredService<IMutableUserContext>());
        builder.Services.AddHostedService<OutboxMessageBackgroundService>();
        builder.Services.AddOptions<OutboxOptions>();
        return ValueTask.CompletedTask;
    }
}
