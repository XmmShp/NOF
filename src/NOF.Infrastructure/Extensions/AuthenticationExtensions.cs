using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NOF.Infrastructure;

namespace NOF.Hosting;

public static partial class NOFInfrastructureExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddAuthenticationResourceServer(Action<AuthenticationResourceServerOptions> configureOptions)
        {
            builder.Services.Configure(configureOptions);

            builder.Services.AddHttpClient<HttpAuthorizationServerService>();
            builder.Services.TryAddScoped<IPermissionResolver, ScopePermissionResolver>();
            builder.Services.TryAddScoped<IJwksService>(static serviceProvider =>
                serviceProvider.GetRequiredService<HttpAuthorizationServerService>());
            builder.Services.TryAddScoped<IAuthorizationServerMetadataService>(static serviceProvider =>
                serviceProvider.GetRequiredService<HttpAuthorizationServerService>());
            builder.Services.TryAddScoped<IJwtTokenExchangeService>(static serviceProvider =>
                serviceProvider.GetRequiredService<HttpAuthorizationServerService>());
            builder.Services.TryAddScoped<IClientCredentialsTokenService>(static serviceProvider =>
                serviceProvider.GetRequiredService<HttpAuthorizationServerService>());
            builder.Services.ReplaceOrAddSingleton<ResourceServerJwksCacheService, ResourceServerJwksCacheService>();
            builder.Services.AddRequestInboundMiddleware<AuthenticationResourceServerInboundMiddleware>();
            builder.Services.AddCommandInboundMiddleware<AuthenticationResourceServerInboundMiddleware>();
            builder.Services.AddNotificationInboundMiddleware<AuthenticationResourceServerInboundMiddleware>();
            return builder;
        }

    }
}
