using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NOF;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddHostedService(Func<IServiceProvider, CancellationToken, Task> startAction)
        {
            return services.AddHostedService(sp => new DelegateHostedService(sp, startAction));
        }

        public IServiceCollection AddHostedService(Action<IServiceProvider, CancellationToken> startAction)
            => services.AddHostedService((sp, ct) => { startAction(sp, ct); return Task.CompletedTask; });

        public OptionsBuilder<TOptions> AddOptionsInConfiguration<TOptions>(string? configSectionPath = null) where TOptions : class
        {
            // ReSharper disable once InvertIf
            if (string.IsNullOrEmpty(configSectionPath))
            {
                configSectionPath = string.GetSectionNameFromOptions<TOptions>();
            }

            return services.AddOptions<TOptions>()
                .BindConfiguration(configSectionPath)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }
    }
}

internal sealed class DelegateHostedService : BackgroundService
{
    private readonly Func<IServiceProvider, CancellationToken, Task> _startAction;
    private readonly IServiceProvider _serviceProvider;

    public DelegateHostedService(IServiceProvider serviceProvider, Func<IServiceProvider, CancellationToken, Task> startAction)
    {
        _serviceProvider = serviceProvider;
        _startAction = startAction ?? throw new ArgumentNullException(nameof(startAction));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _startAction(_serviceProvider, stoppingToken);
    }
}