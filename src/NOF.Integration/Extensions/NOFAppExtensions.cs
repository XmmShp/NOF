using Microsoft.AspNetCore.Builder;

namespace NOF;

public static partial class __NOF_Infrastructure_Extensions__
{
    extension(INOFApp app)
    {
        public INOFApp AddRegistrationConfigurator(Func<INOFApp, ValueTask> func)
            => app.AddRegistrationConfigurator(new DelegateRegistrationConfigurator(func));

        public INOFApp AddStartupConfigurator(Func<INOFApp, WebApplication, Task> func)
            => app.AddStartupConfigurator(new DelegateStartupConfigurator(func));

        public INOFApp AddRegistrationConfigurator<T>() where T : IRegistrationConfigurator, new()
            => app.AddRegistrationConfigurator(new T());

        public INOFApp AddStartupConfigurator<T>() where T : IStartupConfigurator, new()
            => app.AddStartupConfigurator(new T());

        public INOFApp RemoveRegistrationConfigurator<T>() where T : IRegistrationConfigurator
            => app.RemoveRegistrationConfigurator(t => t is T);

        public INOFApp RemoveStartupConfigurator<T>() where T : IStartupConfigurator
            => app.RemoveStartupConfigurator(t => t is T);
    }
}

internal class DelegateStartupConfigurator : IBusinessConfigurator
{
    private readonly Func<INOFApp, WebApplication, Task> _fn;

    public DelegateStartupConfigurator(Func<INOFApp, WebApplication, Task> func)
    {
        _fn = func;
    }

    public Task ExecuteAsync(INOFApp app, WebApplication webApp)
    {
        return _fn(app, webApp);
    }
}

internal class DelegateRegistrationConfigurator : IConfiguredServicesConfigurator
{
    private readonly Func<INOFApp, ValueTask> _fn;

    public DelegateRegistrationConfigurator(Func<INOFApp, ValueTask> func)
    {
        _fn = func;
    }

    public ValueTask ExecuteAsync(INOFApp app)
    {
        return _fn(app);
    }
}