using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Abstraction;

namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddHostingDefaults()
        {
            builder.Services.TryAddSingleton(builder.Environment);

            var registry = builder.GetOrAddRegistry();
            builder.Services.GetOrAddSingleton(() => registry.AutoInjectRegistry);
            builder.Services.GetOrAddSingleton(() => registry.EventHandlerRegistry);
            builder.Services.GetOrAddSingleton<RequestOutboundPipelineTypes>();
            builder.Services.TryAddScoped<RequestOutboundPipelineExecutor>();
            builder.Services.TryAddScoped<IUserContext, UserContext>();
            builder.Services.TryAddScoped<IEventPublisher, InMemoryEventPublisher>();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IDaemonService, EventPublisherAmbientDaemonService>());
            builder.Services.TryAddTransient(typeof(Lazy<>), typeof(NOFLazy<>));
            return builder;
        }
    }
}
