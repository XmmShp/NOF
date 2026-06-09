namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public interface IOAuthAuthorizationHandler
{
    ValueTask<OAuthAuthorizationResult> AuthorizeAsync(
        OAuthAuthorizationRequest request,
        CancellationToken cancellationToken);
}

public interface IOAuthAuthorizationCodeService
{
    ValueTask<string> CreateAsync(
        OAuthAuthorizationCodeDescriptor descriptor,
        CancellationToken cancellationToken);
}

public interface IOAuthSubjectService
{
    ValueTask<OAuthSubjectProfile?> GetProfileAsync(
        string subject,
        IReadOnlySet<string> scopes,
        CancellationToken cancellationToken);

    ValueTask<bool> CanRefreshAsync(
        string subject,
        string refreshTokenId,
        IReadOnlySet<string> scopes,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(true);
}
