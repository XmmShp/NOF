namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class OidcServerEndpointMappingState
{
    private int _isMapped;

    public bool TryMarkMapped() => Interlocked.Exchange(ref _isMapped, 1) == 0;
}
