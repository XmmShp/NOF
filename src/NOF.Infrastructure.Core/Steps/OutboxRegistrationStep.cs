using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers outbox-related services including the background service for outbox command processing,
/// and deferred command/notification senders.
/// </summary>
public class OutboxRegistrationStep : IBaseSettingsServiceRegistrationStep<OutboxRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.AddHostedService<OutboxMessageBackgroundService>();

        builder.Services.AddScoped<IDeferredCommandSender, DeferredCommandSender>();
        builder.Services.AddScoped<IDeferredNotificationPublisher, DeferredNotificationPublisher>();

        return ValueTask.CompletedTask;
    }
}
