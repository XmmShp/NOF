namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class OidcServerBootstrapOptions
{
    public IList<CreateOAuthClientRequest> PublicClients { get; } = [];

    public IList<CreateOAuthClientRequest> ConfidentialClients { get; } = [];
}
