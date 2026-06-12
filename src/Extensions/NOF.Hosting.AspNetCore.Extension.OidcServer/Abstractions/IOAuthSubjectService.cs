namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

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
