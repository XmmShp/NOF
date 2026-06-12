using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Hosting;

namespace NOF.Infrastructure;

public static partial class NOFInfrastructureExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddAuthenticationResourceServer(Action<AuthenticationResourceServerOptions> configureOptions)
        {
            builder.Services.Configure(configureOptions);

            builder.Services.AddHttpClient<HttpJwksService>();
            builder.Services.TryAddScoped<IJwksService>(static serviceProvider =>
                serviceProvider.GetRequiredService<HttpJwksService>());
            builder.Services.TryAddScoped<IAuthorizationServerMetadataService>(static serviceProvider =>
            {
                var jwksService = serviceProvider.GetRequiredService<IJwksService>();
                return jwksService as IAuthorizationServerMetadataService
                    ?? NullAuthorizationServerMetadataService.Instance;
            });
            builder.Services.ReplaceOrAddSingleton<ResourceServerJwksCacheService, ResourceServerJwksCacheService>();
            builder.Services.AddRequestInboundMiddleware<AuthenticationResourceServerInboundMiddleware>();
            builder.Services.AddCommandInboundMiddleware<AuthenticationResourceServerInboundMiddleware>();
            builder.Services.AddNotificationInboundMiddleware<AuthenticationResourceServerInboundMiddleware>();
            return builder;
        }

    }

    private sealed class NullAuthorizationServerMetadataService : IAuthorizationServerMetadataService
    {
        public static readonly NullAuthorizationServerMetadataService Instance = new();

        public Task<OAuthAuthorizationServerMetadataDocument?> GetMetadataAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult<OAuthAuthorizationServerMetadataDocument?>(null);
        }
    }
}
