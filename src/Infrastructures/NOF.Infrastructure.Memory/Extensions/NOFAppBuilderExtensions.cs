using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure.Memory;

public static class NOFInfrastructureMemoryExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddMemoryInfrastructure()
        {
            builder.Services.ReplaceOrAddCacheService<MemoryCacheService>();

            builder.Services.ReplaceOrAddScoped<IEventPublisher, EventPublisher>();
            builder.Services.ReplaceOrAddSingleton<MemoryPersistenceStore, MemoryPersistenceStore>();
            builder.Services.ReplaceOrAddScoped<IMemoryPersistenceContextFactory, MemoryPersistenceContextFactory>();
            builder.Services.ReplaceOrAddScoped(sp => sp.GetRequiredService<IMemoryPersistenceContextFactory>().CreateContext());
            builder.Services.ReplaceOrAddScoped<IUnitOfWork, MemoryUnitOfWork>();
            builder.Services.ReplaceOrAddScoped<ITransactionManager, MemoryTransactionManager>();
            builder.Services.ReplaceOrAddScoped<IInboxMessageRepository, MemoryInboxMessageRepository>();
            builder.Services.ReplaceOrAddScoped<ITenantRepository, MemoryTenantRepository>();
            builder.Services.ReplaceOrAddScoped<IOutboxMessageRepository, MemoryOutboxMessageRepository>();
            builder.Services.ReplaceOrAddScoped<IStateMachineContextRepository, MemoryStateMachineContextRepository>();

            builder.Services.ReplaceOrAddSingleton<ICommandRider, MemoryCommandRider>();
            builder.Services.ReplaceOrAddSingleton<INotificationRider, MemoryNotificationRider>();

            builder.Services.AddHostedService<MemoryPersistenceWarningHostedService>();

            return builder;
        }
    }
}
