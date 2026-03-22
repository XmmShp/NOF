using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

namespace NOF.Infrastructure.Memory;

public static class NOFInfrastructureMemoryExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddMemoryInfrastructure()
        {
            builder.Services.AddCacheService<MemoryCacheService>();

            builder.Services.AddScoped<IEventPublisher, EventPublisher>();
            builder.Services.AddSingleton<MemoryPersistenceStore>();
            builder.Services.AddScoped<MemoryPersistenceSession>();
            builder.Services.AddScoped<IUnitOfWork, MemoryUnitOfWork>();
            builder.Services.AddScoped<ITransactionManager, MemoryTransactionManager>();
            builder.Services.AddScoped<IInboxMessageRepository, MemoryInboxMessageRepository>();
            builder.Services.AddScoped<IOutboxMessageRepository, MemoryOutboxMessageRepository>();
            builder.Services.AddScoped<ITenantRepository, MemoryTenantRepository>();
            builder.Services.AddScoped<IStateMachineContextRepository, MemoryStateMachineContextRepository>();
            builder.Services.AddScoped<ICommandRider, MemoryCommandRider>();
            builder.Services.AddScoped<INotificationRider, MemoryNotificationRider>();
            builder.Services.AddScoped<IRequestRider, MemoryRequestRider>();

            builder.Services.AddHostedService<MemoryPersistenceWarningHostedService>();

            return builder;
        }
    }
}
