using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Abstraction;
using NOF.Hosting;
using System.Text.Json;

namespace NOF.Infrastructure.Extension.Authentication;

public static partial class NOFAuthenticationExtensions
{
    private static int _jsonInitialized;

    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddAuthenticationResourceServer(Action<AuthenticationResourceServerOptions> configureOptions)
        {
            EnsureJsonRegistered();
            builder.Services.Configure(configureOptions);

            builder.Services.AddHttpClient<HttpJwksService>();
            builder.Services.TryAddScoped<IJwksService>(static serviceProvider =>
                serviceProvider.GetRequiredService<HttpJwksService>());
            builder.Services.ReplaceOrAddSingleton<ResourceServerJwksCacheService, ResourceServerJwksCacheService>();
            builder.Services.AddRequestInboundMiddleware<AuthenticationResourceServerInboundMiddleware>();
            return builder;
        }
    }

    private static void EnsureJsonRegistered()
    {
        if (Interlocked.Exchange(ref _jsonInitialized, 1) == 1)
        {
            return;
        }

        JsonSerializerOptions.ConfigureNOFJsonSerializerOptions(options =>
        {
            options.TypeInfoResolverChain.Add(NOFAuthenticationJsonSerializerContext.Default);
        });
    }
}
