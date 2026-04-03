using Microsoft.Extensions.Options;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed class JwtResourceServerOptionsValidator : IValidateOptions<JwtResourceServerOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtResourceServerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.JwksEndpoint))
        {
            return ValidateOptionsResult.Fail("JwtResourceServerOptions.JwksEndpoint must be configured.");
        }

        if (!Uri.TryCreate(options.JwksEndpoint, UriKind.Absolute, out var uri))
        {
            return ValidateOptionsResult.Fail("JwtResourceServerOptions.JwksEndpoint must be an absolute URI.");
        }

        if (options.RequireHttpsMetadata && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail("JwtResourceServerOptions.JwksEndpoint must use HTTPS when RequireHttpsMetadata is enabled.");
        }

        return ValidateOptionsResult.Success;
    }
}
