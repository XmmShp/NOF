using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace NOF;

public static partial class __NOF_Infrastructure_MassTransit_Extensions__
{
    extension(INOFApp app)
    {
        public INOFMassTransitSelector AddMassTransit()
            => app.AddMassTransit(AppDomain.CurrentDomain.GetAssemblies());

        public INOFMassTransitSelector AddMassTransit(params Type[] types)
        {
            var assemblies = new HashSet<Assembly>();
            if (Assembly.GetEntryAssembly() is { } assembly)
            {
                assemblies.Add(assembly);
            }
            foreach (var type in types)
            {
                assemblies.Add(type.Assembly);
            }

            return app.AddMassTransit(assemblies.ToArray());
        }
        public INOFMassTransitSelector AddMassTransit(IEnumerable<Assembly> assemblies)
        {
            app.Services.AddScoped<ICommandSender, CommandSender>();
            app.Services.AddScoped<IEventPublisher, EventPublisher>();
            app.Services.AddScoped<INotificationPublisher, NotificationPublisher>();
            app.Services.AddScoped<IRequestSender, RequestSender>();
            app.AddRegistrationConfigurator(new MassTransitConfigurator(assemblies));
            return new NOFMassTransitSelector(app);
        }
    }
}
