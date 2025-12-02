using Microsoft.AspNetCore.Builder;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NOF.Infrastructure.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]

namespace NOF;

public interface INOFApp
{
    INOFApp AddRegistrationTask(IRegistrationTask task);
    INOFApp AddStartupTask(IStartupTask task);
    INOFApp RemoveRegistrationTask(Predicate<IRegistrationTask> predictor);
    INOFApp RemoveStartupTask(Predicate<IStartupTask> predictor);
    Task<WebApplication> BuildAsync();
}

public class NOFApp : INOFApp
{
    private readonly HashSet<IRegistrationTask> _registrationTasks = [];
    private readonly HashSet<IStartupTask> _startupTasks = [];
    private readonly Dictionary<string, object?> _metadata = [];
    private readonly WebApplicationBuilder _builder;

    public INOFApp AddRegistrationTask(IRegistrationTask task)
    {
        _registrationTasks.Add(task);
        return this;
    }

    public INOFApp AddStartupTask(IStartupTask task)
    {
        _startupTasks.Add(task);
        return this;
    }

    public INOFApp RemoveRegistrationTask(Predicate<IRegistrationTask> predictor)
    {
        _registrationTasks.RemoveWhere(predictor);
        return this;
    }

    public INOFApp RemoveStartupTask(Predicate<IStartupTask> predictor)
    {
        _startupTasks.RemoveWhere(predictor);
        return this;
    }


    public async Task<WebApplication> BuildAsync()
    {
        var regGraph = new TaskGraph(_registrationTasks);
        foreach (var task in regGraph.GetExecutionOrder())
        {
            if (task is not IRegistrationTask registrationTask)
            {
                continue;
            }

            await registrationTask.ExecuteAsync(new RegistrationArgs(_builder, _metadata));
        }

        var app = _builder.Build();
        var startGraph = new TaskGraph(_startupTasks);

        foreach (var task in startGraph.GetExecutionOrder())
        {
            if (task is not IStartupTask startupTask)
            {
                continue;
            }

            await startupTask.ExecuteAsync(new StartupArgs(app, _metadata));
        }

        return app;
    }

    internal NOFApp(string[] args)
    {
        _builder = WebApplication.CreateBuilder(args);
    }

    public static INOFApp CreateApp(string[] args)
    {
        var builder = new NOFApp(args);
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        builder._metadata.Assemblies.Add(assembly);
        return builder;
    }
}