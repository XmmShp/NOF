using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers the handler pipeline infrastructure including pipeline configuration actions
/// and the handler executor.
/// </summary>
public class HandlerPipelineRegistrationStep : IBaseSettingsServiceRegistrationStep
{
    public ValueTask ExecuteAsync(INOFAppBuilder builder)
    {
        // Ensure the collection of pipeline configuration actions is registered
        if (builder.Services.All(d => d.ServiceType != typeof(IEnumerable<Action<IHandlerPipelineBuilder, IServiceProvider>>)))
        {
            builder.Services.AddSingleton<IEnumerable<Action<IHandlerPipelineBuilder, IServiceProvider>>>(sp =>
            {
                var actions = sp.GetService<List<Action<IHandlerPipelineBuilder, IServiceProvider>>>();
                return actions ?? [];
            });
            builder.Services.AddSingleton(new List<Action<IHandlerPipelineBuilder, IServiceProvider>>());
        }
        builder.Services.AddScoped<IHandlerExecutor, HandlerExecutor>();

        return ValueTask.CompletedTask;
    }
}
