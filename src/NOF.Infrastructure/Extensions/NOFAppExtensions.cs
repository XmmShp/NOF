using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace NOF;

public static partial class __NOF_Infrastructure_Extensions__
{
    extension(INOFApp app)
    {
        public IServiceCollection Services => app.Unwarp().Services;

        public INOFApp AddCombinedConfigurator(ICombinedConfigurator configurator)
        {
            app.AddRegistrationConfigurator(configurator);
            app.AddStartupConfigurator(configurator);
            return app;
        }

        public INOFApp AddRegistrationConfigurator(Func<RegistrationArgs, ValueTask> func)
            => app.AddRegistrationConfigurator(new DelegateRegistrationConfigurator(func));

        public INOFApp AddStartupConfigurator(Func<StartupArgs, Task> func)
            => app.AddStartupConfigurator(new DelegateStartupConfigurator(func));

        public INOFApp AddRegistrationConfigurator<T>() where T : IRegistrationConfigurator, new()
            => app.AddRegistrationConfigurator(new T());

        public INOFApp AddStartupConfigurator<T>() where T : IStartupConfigurator, new()
            => app.AddStartupConfigurator(new T());

        public INOFApp AddCombinedConfigurator<T>() where T : ICombinedConfigurator, new()
            => app.AddCombinedConfigurator(new T());

        public INOFApp RemoveRegistrationConfigurator<T>() where T : IRegistrationConfigurator
            => app.RemoveRegistrationConfigurator(t => t is T);

        public INOFApp RemoveStartupConfigurator<T>() where T : IStartupConfigurator
            => app.RemoveStartupConfigurator(t => t is T);

        public INOFApp RemoveCombinedConfigurator<T>() where T : ICombinedConfigurator
            => app.RemoveRegistrationConfigurator(t => t is T)
                .RemoveStartupConfigurator(t => t is T);

        public INOFApp AddAssembly<T>()
            => app.AddAssembly(typeof(T));

        public INOFApp AddAssembly(Type type)
            => app.AddAssembly(type.Assembly);

        public INOFApp AddAssembly(Assembly assembly)
        {
            app.Metadata.Assemblies.Add(assembly);
            return app;
        }

        public INOFApp UseDefaultSettings()
        {
            app.AddRegistrationConfigurator<ConfigureJsonOptionsConfigurator>();
            app.AddRegistrationConfigurator<AddMassTransitConfigurator>();
            app.AddRegistrationConfigurator<AddDefaultServicesConfigurator>();
            app.AddRegistrationConfigurator<AddSignalRConfigurator>();
            app.AddRegistrationConfigurator<AddRedisDistributedCacheConfigurator>();

            app.AddCombinedConfigurator<AddCorsConfigurator>();
            app.AddCombinedConfigurator<AddApiResponseMiddlewareConfigurator>();
            app.AddCombinedConfigurator<AddJwtAuthenticationConfigurator>();
            app.AddCombinedConfigurator<AddAspireConfigurator>();

            if (app.Unwarp().Environment.IsDevelopment())
            {
                app.AddCombinedConfigurator<AddScalarConfigurator>();
            }
            return app;
        }
    }
}

internal class DelegateStartupConfigurator : IBusinessConfigurator
{
    private readonly Func<StartupArgs, Task> _fn;

    public DelegateStartupConfigurator(Func<StartupArgs, Task> func)
    {
        _fn = func;
    }

    public Task ExecuteAsync(StartupArgs args)
    {
        return _fn(args);
    }
}

internal class DelegateRegistrationConfigurator : IConfiguredServicesConfigurator
{
    private readonly Func<RegistrationArgs, ValueTask> _fn;

    public DelegateRegistrationConfigurator(Func<RegistrationArgs, ValueTask> func)
    {
        _fn = func;
    }

    public ValueTask ExecuteAsync(RegistrationArgs args)
    {
        return _fn(args);
    }
}