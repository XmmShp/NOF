using System.Reflection;

namespace NOF;

public static class NOFAppBuilderExtensions
{
    extension(INOFApp app)
    {
        public INOFApp AddRegistrationTask(Func<RegistrationArgs, Task> func)
        {
            return app.AddRegistrationTask(new DelegateRegistrationTask(func));
        }

        public INOFApp AddStartupTask(Func<StartupArgs, Task> func)
        {
            return app.AddStartupTask(new DelegateStartupTask(func));
        }

        public INOFApp AddRegistrationTask<T>() where T : IRegistrationTask, new()
        {
            return app.AddRegistrationTask(new T());
        }

        public INOFApp AddStartupTask<T>() where T : IStartupTask, new()
        {
            return app.AddStartupTask(new T());
        }

        public INOFApp RemoveRegistrationTask<T>() where T : IRegistrationTask
        {
            var type = typeof(T);
            return app.RemoveRegistrationTask(t => type.IsInstanceOfType(t));
        }

        public INOFApp RemoveStartupTask<T>() where T : IStartupTask
        {
            var type = typeof(T);
            return app.RemoveStartupTask(t => type.IsInstanceOfType(t));
        }

        public INOFApp AddAssembly<T>()
            => app.AddAssembly(typeof(T));

        public INOFApp AddAssembly(Type type)
            => app.AddAssembly(type.Assembly);

        public INOFApp AddAssembly(Assembly assembly)
        {
            app.AddRegistrationTask(args =>
            {
                args.Metadata.Assemblies.Add(assembly);
                return Task.CompletedTask;
            });
            return app;
        }

        public INOFApp UseDefaultSettings()
        {
            app.AddRegistrationTask<ConfigureJsonOptionsTask>();
            app.AddRegistrationTask<AddMassTransitTask>();
            app.AddRegistrationTask<AddDefaultServicesTask>();

            app.AddRegistrationTask<AddApiResponseMiddlewareTask>();
            app.AddStartupTask<UseApiResponseTask>();

            app.AddRegistrationTask<AddJwtTask>();
            app.AddRegistrationTask<ConfigureJwtTask>();
            app.AddStartupTask<UseJwtTask>();

            app.AddRegistrationTask<AddSignalRTask>();

            app.AddRegistrationTask<AddCorsTask>();
            app.AddStartupTask<UseCorsTask>();

            app.AddRegistrationTask<AddRedisDistributedCacheTask>();

            app.AddRegistrationTask<AddScalarTask>();
            app.AddStartupTask<UseScalarTask>();

            app.AddRegistrationTask<AddAspireTask>();
            app.AddStartupTask<UseAspireTask>();
            return app;
        }

        public INOFApp UsePostgreSQL<TDbContext>(bool autoMigrate = false) where TDbContext : NOFDbContext
        {
            app.AddRegistrationTask<AddPostgreSQLTask<TDbContext>>();
            if (autoMigrate)
            {
                app.AddStartupTask<MigrationTask>();
            }
            return app;
        }
    }
}

internal class DelegateStartupTask : IStartupTask
{
    private readonly Func<StartupArgs, Task> _fn;

    public DelegateStartupTask(Func<StartupArgs, Task> func)
    {
        _fn = func;
    }

    public Task ExecuteAsync(StartupArgs args)
    {
        return _fn(args);
    }
}

internal class DelegateRegistrationTask : IRegistrationTask
{
    private readonly Func<RegistrationArgs, Task> _fn;

    public DelegateRegistrationTask(Func<RegistrationArgs, Task> func)
    {
        _fn = func;
    }

    public Task ExecuteAsync(RegistrationArgs args)
    {
        return _fn(args);
    }
}