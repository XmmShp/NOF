using NOF.Hosting;
using NOF.Infrastructure.Extension.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public static partial class NOFOidcServerExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddOidcServer(Action<OAuthAuthorizationServerOptions> configureOptions)
        {
            builder.Services.Configure(configureOptions);
            builder.Services.TryAddScoped<IOAuthAuthorizationCodeService, OAuthAuthorizationCodeService>();
            return builder;
        }
    }
}
