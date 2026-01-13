using MassTransit;

namespace NOF;

public static partial class __NOF_Infrastructure_MassTransit_EntityFrameworkCore_Extensions__
{
    internal interface IUseEFCoreOutboxInvokeDelegate
    {
        INOFMassTransitSelector Invoke(INOFMassTransitSelector selector, Action<IEntityFrameworkOutboxConfigurator> configurator);
    }

    internal class UseEFCoreOutboxInvokeDelegate<TDbContext> : IUseEFCoreOutboxInvokeDelegate
        where TDbContext : NOFDbContext
    {
        public INOFMassTransitSelector Invoke(INOFMassTransitSelector selector, Action<IEntityFrameworkOutboxConfigurator> configurator)
        {
            return selector.UseEFCoreOutbox<TDbContext>(configurator);
        }
    }

    internal class RegistrationStepWrapper : ServiceRegistrationStep, IBefore<IDependentServiceRegistrationStep>
    {
        public RegistrationStepWrapper(Func<INOFAppBuilder, ValueTask> configurator) : base(configurator)
        {
        }
    }

    extension(INOFMassTransitSelector selector)
    {

        public INOFMassTransitSelector UseEFCoreOutbox(Action<IEntityFrameworkOutboxConfigurator> configurator)
        {
            ArgumentNullException.ThrowIfNull(configurator);
            selector.Builder.AddRegistrationStep(new RegistrationStepWrapper(builder =>
            {
                var dbContextType = builder.DbContextType;
                ArgumentNullException.ThrowIfNull(dbContextType);

                var invokeDelegateType = typeof(UseEFCoreOutboxInvokeDelegate<>).MakeGenericType(dbContextType);
                var invokeDelegate = (IUseEFCoreOutboxInvokeDelegate)Activator.CreateInstance(invokeDelegateType)!;
                invokeDelegate.Invoke(selector, configurator);
                return ValueTask.CompletedTask;
            }));
            return selector;
        }

        public INOFMassTransitSelector UseEFCoreOutbox<TDbContext>(Action<IEntityFrameworkOutboxConfigurator> configurator)
            where TDbContext : NOFDbContext
        {
            ArgumentNullException.ThrowIfNull(configurator);

            selector.Builder.StartupEventChannel.Subscribe<DbContextModelCreating>(e => e.Builder.AddTransactionalOutboxEntities());
            selector.Builder.StartupEventChannel.Subscribe<MassTransitConfiguring>(e =>
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
