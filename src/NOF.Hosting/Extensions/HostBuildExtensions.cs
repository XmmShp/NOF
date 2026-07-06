using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public Task<THostApplication> BuildNOFAsync<THostApplication>(Func<THostApplication> buildApplication)
            where THostApplication : class, IHost
        {
            ArgumentNullException.ThrowIfNull(buildApplication);
            return builder.BuildNOFAsync(() => Task.FromResult(buildApplication()));
        }

        public async Task<THostApplication> BuildNOFAsync<THostApplication>(Func<Task<THostApplication>> buildApplicationAsync)
            where THostApplication : class, IHost
        {
            ArgumentNullException.ThrowIfNull(buildApplicationAsync);

            builder.Services.TryAddSingleton(builder.Environment);
            builder.Services.AddNOFHosting();

            var app = await buildApplicationAsync().ConfigureAwait(false);
            await app.InitializeNOFAsync().ConfigureAwait(false);
            return app;
        }
    }

    extension(IHost host)
    {
        public async Task<IHost> InitializeNOFAsync()
        {
            var startGraph = new DependencyGraph<IApplicationInitializationStep>(
                host.Services.GetServices<IApplicationInitializationStep>());
            foreach (var step in startGraph.GetExecutionOrder())
            {
                await step.ExecuteAsync(host).ConfigureAwait(false);
            }

            return host;
        }
    }
}
