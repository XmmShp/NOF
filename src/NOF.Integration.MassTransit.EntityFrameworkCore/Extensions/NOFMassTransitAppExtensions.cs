using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace NOF;

public static partial class __NOF_Infrastructure_MassTransit_EntityFrameworkCore_Extensions__
{
    internal interface IUseEFCoreOutboxInvokeDelegate
    {
        INOFMassTransitSelector Invoke(INOFMassTransitSelector selector, Action<IEntityFrameworkOutboxConfigurator>? configurator = null);
    }
    internal class UseEFCoreOutboxInvokeDelegate<TDbContext> : IUseEFCoreOutboxInvokeDelegate
        where TDbContext : DbContext
    {
        public INOFMassTransitSelector Invoke(INOFMassTransitSelector selector, Action<IEntityFrameworkOutboxConfigurator>? configurator)
        {
            return selector.UseEFCoreOutbox<TDbContext>(configurator);
        }
    }

    extension(INOFMassTransitSelector selector)
    {
        public INOFMassTransitSelector UseEFCoreOutbox(Action<IEntityFrameworkOutboxConfigurator>? configurator = null)
        {
            var dbContextType = selector.App.Metadata.GetOrDefault<Type>("NOF.Infrastructure.EntityFrameworkCore:DbContextType");
            if (dbContextType is null)
            {
                ArgumentNullException.ThrowIfNull(dbContextType);
            }
            var invokeDelegateType = typeof(UseEFCoreOutboxInvokeDelegate<>).MakeGenericType(dbContextType);
            var invokeDelegate = (IUseEFCoreOutboxInvokeDelegate)Activator.CreateInstance(invokeDelegateType)!;
            return invokeDelegate.Invoke(selector, configurator);
        }

        public INOFMassTransitSelector UseEFCoreOutbox<TDbContext>(Action<IEntityFrameworkOutboxConfigurator>? configurator = null)
            where TDbContext : DbContext
        {
            if (configurator is null)
            {
                if (selector.App.Metadata.GetOrDefault<bool>("NOF.Infrastructure.EntityFrameworkCore.PostgreSQL:UsePostgreSQL"))
                {
                    configurator = o => o.UsePostgres();
                }
            }

            ArgumentNullException.ThrowIfNull(configurator);

            EventDispatcher.Subscribe<DbContextModelCreating>(e => e.Builder.AddTransactionalOutboxEntities());
            EventDispatcher.Subscribe<MassTransitConfiguring>(e =>
            {
                var config = e.Configurator;
                config.AddEntityFrameworkOutbox<TDbContext>(o =>
                {
                    configurator(o);
                    o.UseBusOutbox();
                });
                config.AddConfigureEndpointsCallback((context, name, cfg) =>
                {
                    cfg.UseEntityFrameworkOutbox<TDbContext>(context);
                });
            });
            return selector;
        }
    }
}
