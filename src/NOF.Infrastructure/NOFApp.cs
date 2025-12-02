using Microsoft.AspNetCore.Builder;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NOF.Infrastructure.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]

namespace NOF;

public interface INOFApp
{
    INOFApp AddRegistrationConfigurator(IRegistrationConfigurator task);
    INOFApp AddStartupConfigurator(IStartupConfigurator task);
    INOFApp RemoveRegistrationConfigurator(Predicate<IRegistrationConfigurator> predictor);
    INOFApp RemoveStartupConfigurator(Predicate<IStartupConfigurator> predictor);
    IDictionary<string, object?> Metadata { get; }
    WebApplicationBuilder Unwarp();
    Task<WebApplication> BuildAsync();
}

public class NOFApp : INOFApp
{
    internal readonly HashSet<IRegistrationConfigurator> RegistrationStages = [];
    internal readonly HashSet<IStartupConfigurator> StartupStages = [];
    private readonly WebApplicationBuilder _builder;

    public IDictionary<string, object?> Metadata { get; }

    public INOFApp AddRegistrationConfigurator(IRegistrationConfigurator task)
    {
        RegistrationStages.Add(task);
        return this;
    }

    public INOFApp AddStartupConfigurator(IStartupConfigurator task)
    {
        StartupStages.Add(task);
        return this;
    }

    public INOFApp RemoveRegistrationConfigurator(Predicate<IRegistrationConfigurator> predictor)
    {
        RegistrationStages.RemoveWhere(predictor);
        return this;
    }

    public INOFApp RemoveStartupConfigurator(Predicate<IStartupConfigurator> predictor)
    {
        StartupStages.RemoveWhere(predictor);
        return this;
    }

    public WebApplicationBuilder Unwarp()
    {
        return _builder;
    }

    public async Task<WebApplication> BuildAsync()
    {
        var regGraph = new ConfiguratorGraph<IRegistrationConfigurator>(RegistrationStages);
        foreach (var task in regGraph.GetExecutionOrder())
        {
            await task.ExecuteAsync(new RegistrationArgs(_builder, Metadata));
        }

        var app = _builder.Build();
        var startGraph = new ConfiguratorGraph<IStartupConfigurator>(StartupStages);

        foreach (var task in startGraph.GetExecutionOrder())
        {
            await task.ExecuteAsync(new StartupArgs(app, Metadata));
        }

        return app;
    }

    internal NOFApp(string[] args)
    {
        _builder = WebApplication.CreateBuilder(args);
        Metadata = new Dictionary<string, object?>();
    }

    public static INOFApp CreateApp(string[] args)
    {
        var builder = new NOFApp(args);
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        builder.Metadata.Assemblies.Add(assembly);
        return builder;
    }
}