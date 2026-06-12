using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Abstraction;
using NOF.Hosting;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public static partial class NOFOidcServerExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddAuthenticationAuthority(Action<AuthenticationAuthorityOptions> configureOptions)
        {
            builder.Services.Configure(configureOptions);

            builder.TryAddRegistrationStep<PersistedSigningKeyPersistenceRegistrationStep>();
            builder.TryAddRegistrationStep<RevokedRefreshTokenPersistenceRegistrationStep>();
            builder.Services.ReplaceOrAddScoped<LocalJwksService, LocalJwksService>();
            builder.Services.ReplaceOrAddScoped<IJwksService>(static serviceProvider => serviceProvider.GetRequiredService<LocalJwksService>());
            builder.Services.ReplaceOrAddScoped<ITokenService, TokenAuthorityService>();
            builder.Services.AddHostedService<SigningKeyRotationBackgroundService>();

            return builder;
        }

        public INOFAppBuilder AddOidcServer(Action<OAuthAuthorizationServerOptions> configureOptions)
        {
            builder.Services.Configure(configureOptions);
            builder.Services.TryAddScoped<IOAuthAuthorizationCodeService, OAuthAuthorizationCodeService>();
            return builder;
        }
    }
}
