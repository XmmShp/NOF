using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Selector returned by <c>AddJwtAuthorization</c> for fluent configuration chaining.
/// </summary>
public readonly struct JwtAuthorizationSelector
{
    public INOFAppBuilder Builder { get; }

    public JwtAuthorizationSelector(INOFAppBuilder builder)
    {
        Builder = builder;
    }

    /// <summary>
    /// Replaces the default <see cref="HttpJwksProvider"/> with <see cref="RequestSenderJwksProvider"/>
    /// which fetches JWKS via <see cref="NOF.Application.IRequestSender"/>.
    /// </summary>
    /// <returns>This selector for further chaining.</returns>
    public JwtAuthorizationSelector UseRequestJwksProvider()
    {
        Builder.Services.ReplaceOrAddSingleton<IJwksProvider, RequestSenderJwksProvider>();
        return this;
    }
}
