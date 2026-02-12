using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers outbox-related services including the background service for outbox command processing,
/// deferred command/notification senders, and the outbox message collector.
/// </summary>
public class OutboxRegistrationStep : IBaseSettingsServiceRegistrationStep
{
    public ValueTask ExecuteAsync(INOFAppBuilder builder)
    {
        builder.Services.AddHostedService<OutboxCommandBackgroundService>();

        builder.Services.AddScoped<IDeferredCommandSender, DeferredCommandSender>();
        builder.Services.AddScoped<IDeferredNotificationPublisher, DeferredNotificationPublisher>();

        builder.Services.AddScoped<IOutboxMessageCollector, OutboxMessageCollector>();

        return ValueTask.CompletedTask;
    }
}
