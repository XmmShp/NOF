using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace NOF;

/// <summary />
// ReSharper disable once InconsistentNaming
public static partial class NOFInfrastructureMassTransitExtensions
{
    ///
    extension(INOFMassTransitSelector selector)
    {
        public INOFMassTransitSelector UseInMemoryInboxOutbox()
        {
            selector.Builder.StartupEventChannel.Subscribe<MassTransitConfiguring>(e =>
            {
                var config = e.Configurator;
                config.AddInMemoryInboxOutbox();
                config.AddConfigureEndpointsCallback((context, name, cfg) =>
                {
                    cfg.UseInMemoryInboxOutbox(context);
                });
            });
            return selector;
        }

        public INOFMassTransitSelector AddRequestHandleNode(Type nodeType)
        {
            selector.Builder.AddInitializationStep((_, app) =>
            {
                app.Services.GetRequiredService<IRequestHandleNodeRegistry>().Registry.AddFirst(nodeType);
                return Task.CompletedTask;
            });
            return selector;
        }
    }
}
