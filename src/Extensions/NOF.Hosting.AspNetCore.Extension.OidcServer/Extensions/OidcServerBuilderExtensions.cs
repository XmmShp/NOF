using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Hosting.AspNetCore.Extension.OidcServer;
using NOF.Infrastructure;

namespace NOF.Hosting;

public static partial class NOFOidcServerExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddOidcServer(Action<OAuthAuthorizationServerOptions> configureOptions)
        {
            builder.Services.Configure(configureOptions);
            builder.TryAddRegistrationStep<OidcServerPersistenceRegistrationStep>();
            builder.Services.ReplaceOrAddScoped<LocalJwksService, LocalJwksService>();
            builder.Services.ReplaceOrAddScoped<IJwksService>(static serviceProvider => serviceProvider.GetRequiredService<LocalJwksService>());
            builder.Services.ReplaceOrAddScoped<ITokenService, TokenAuthorityService>();
            builder.Services.AddHostedService<SigningKeyRotationBackgroundService>();
            builder.Services.TryAddScoped<IOAuthAuthorizationCodeService, OAuthAuthorizationCodeService>();
            return builder;
        }
    }
}
