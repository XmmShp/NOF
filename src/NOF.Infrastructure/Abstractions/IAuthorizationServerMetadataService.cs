namespace NOF.Infrastructure;

public interface IAuthorizationServerMetadataService
{
    Task<OAuthAuthorizationServerMetadataDocument?> GetMetadataAsync(CancellationToken cancellationToken = default);
}
