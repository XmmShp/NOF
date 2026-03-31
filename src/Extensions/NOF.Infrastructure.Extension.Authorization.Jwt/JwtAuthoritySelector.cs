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

    public JwtAuthoritySelector AddJwksRequestHandler() => this;
}
