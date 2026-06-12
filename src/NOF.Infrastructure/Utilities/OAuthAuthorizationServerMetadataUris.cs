namespace NOF.Infrastructure;

public static class OAuthAuthorizationServerMetadataUris
{
    private const string OAuthAuthorizationServerMetadataPath = ".well-known/oauth-authorization-server";

    public static Uri BuildMetadataEndpoint(string issuer, bool requireHttps)
    {
        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new InvalidOperationException("Authentication resource server authorization server is not configured.");
        }

        var issuerUri = new Uri(NormalizeIssuer(issuer), UriKind.Absolute);
        if (requireHttps && issuerUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Authentication resource server authorization server metadata must use HTTPS.");
        }

        if (!string.IsNullOrEmpty(issuerUri.Query) || !string.IsNullOrEmpty(issuerUri.Fragment))
        {
            throw new InvalidOperationException("Authentication resource server authorization server must not contain query or fragment components.");
        }

        var path = issuerUri.AbsolutePath is "/" or ""
            ? $"/{OAuthAuthorizationServerMetadataPath}"
            : $"/{OAuthAuthorizationServerMetadataPath}{issuerUri.AbsolutePath.TrimEnd('/')}";

        return new UriBuilder(issuerUri)
        {
            Path = path,
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri;
    }

    public static string NormalizeIssuer(string issuer)
        => issuer.TrimEnd('/');
}
