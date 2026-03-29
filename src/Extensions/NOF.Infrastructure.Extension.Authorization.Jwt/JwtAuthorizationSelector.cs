using NOF.Hosting;

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
    /// Replaces the default <see cref="HttpJwksProvider"/> with <see cref="RequestDispatcherJwksProvider"/>
    /// which fetches JWKS via in-process dispatch.
    /// </summary>
    /// <returns>This selector for further chaining.</returns>
    public JwtAuthorizationSelector UseRequestJwksProvider()
    {
        Builder.Services.ReplaceOrAddSingleton<IJwksProvider, RequestDispatcherJwksProvider>();
        return this;
    }
}
