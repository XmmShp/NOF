using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NOF;

/// <summary />
// ReSharper disable once InconsistentNaming
public static partial class __NOF_Infrastructure_MassTransit_Extensions__
{
    ///
    extension<THostApplication>(INOFMassTransitSelector<THostApplication> selector)
        where THostApplication : class, IHost
    {
        public INOFMassTransitSelector<THostApplication> UseInMemoryInboxOutbox()
        {
            selector.Builder.EventDispatcher.Subscribe<MassTransitConfiguring>(e =>
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

        public INOFMassTransitSelector<THostApplication> AddRequestHandleNode(Type nodeType)
        {
            selector.Builder.AddApplicationConfig((_, app) =>
            {
                app.Services.GetRequiredService<IRequestHandleNodeRegistry>().Registry.AddFirst(nodeType);
                return Task.CompletedTask;
            });
            return selector;
        }
    }
}
