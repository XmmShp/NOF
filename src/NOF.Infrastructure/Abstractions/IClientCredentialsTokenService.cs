namespace NOF.Infrastructure;

public interface IClientCredentialsTokenService
{
    ValueTask<ClientCredentialsTokenResponse> GetTokenAsync(
        ClientCredentialsTokenRequest request,
        CancellationToken cancellationToken = default);
}
