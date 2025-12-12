using MassTransit;

namespace NOF;

public static partial class __NOF_Infrastructure_MassTransit_Extensions__
{
    extension(INOFMassTransitSelector selector)
    {
        public INOFMassTransitSelector UseInMemoryInboxOutbox()
        {
            EventDispatcher.Subscribe<MassTransitConfiguring>(e =>
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
    }
}
