using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Abstraction;
using NOF.Hosting;
using System.Text.Json;

namespace NOF.Infrastructure;

public static partial class NOFInfrastructureExtensions
{
    private static int _authenticationJsonInitialized;

    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddAuthenticationResourceServer(Action<AuthenticationResourceServerOptions> configureOptions)
        {
            EnsureAuthenticationJsonRegistered();
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
            builder.Services.AddCommandOutboundMiddleware<JwtTokenPropagationOutboundMiddleware>();
            builder.Services.AddNotificationOutboundMiddleware<JwtTokenPropagationOutboundMiddleware>();
            return builder;
        }
    }

    private static void EnsureAuthenticationJsonRegistered()
    {
        if (Interlocked.Exchange(ref _authenticationJsonInitialized, 1) == 1)
        {
            return;
        }

        JsonSerializerOptions.ConfigureNOFJsonSerializerOptions(options =>
        {
            options.TypeInfoResolverChain.Add(NOFAuthenticationJsonSerializerContext.Default);
        });
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
