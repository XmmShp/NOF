using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Infrastructure.Core;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Selector returned by <c>AddJwtAuthority</c> for fluent configuration chaining.
/// </summary>
public readonly struct JwtAuthoritySelector
{
    public INOFAppBuilder Builder { get; }

    public JwtAuthoritySelector(INOFAppBuilder builder)
    {
        Builder = builder;
    }

    /// <summary>
    /// Registers a <see cref="GetJwks"/> so that JWKS can be retrieved via <see cref="IRequestSender"/>.
    /// </summary>
    /// <returns>This selector for further chaining.</returns>
    public JwtAuthoritySelector AddJwksRequestHandler()
    {
#pragma warning disable CS8620
        Builder.Services.AddHandlerInfo(
            new HandlerInfo(HandlerKind.RequestWithResponse, typeof(GetJwks), typeof(GetJwksRequest), typeof(GetJwksResponse)));
#pragma warning restore CS8620

        return this;
    }
}
